using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class NetworkSyncView : NetworkBehaviour
{
    private World _world;
    private EntityId _entity;
    private bool _isServerInitialized = false;

    private PlayerRespawnUI respawnUI;

    private NetworkVariable<NetworkTransformState> _netTransform = new(
        writePerm: NetworkVariableWritePermission.Server
    );

    private NetworkVariable<NetworkHealthState> _netHealth = new(writePerm: NetworkVariableWritePermission.Server);

    private NetworkVariable<CombatState> _netCombatState = new(writePerm: NetworkVariableWritePermission.Server);

    private NetworkVariable<NetworkMovementState> _netMovement = new(writePerm: NetworkVariableWritePermission.Server);

    private NetworkVariable<NetworkScoreState> _netScore = new(writePerm: NetworkVariableWritePermission.Server);

    // Weapon configuration - configured directly on each character prefab
    // Same approach as EnemyNetworkSyncView for consistency
    [Header("Weapon Configuration (Configure on Prefab)")]
    [SerializeField]
    private AttackExecutionType _executionType;

    [SerializeField]
    private GameObject _projectilePrefab;

    [SerializeField]
    private ParticleSystem _hitImpactParticlePrefab;

    [SerializeField]
    private float _projectileSpeed = 15f;

    [SerializeField]
    private float _projectileLifetime = 5f;

    [SerializeField]
    private Vector3 _projectileSpawnOffset;

    [SerializeField]
    private string _attackAnimationTrigger = "attack";

    [SerializeField]
    private int _totalAttackAnimations = 1;

    [SerializeField]
    private float _baseDamage = 10f;

    [SerializeField]
    private float _baseCooldown = 1.2f;

    [SerializeField]
    private float _baseRange = 2f;

    [Header("Character Lookup (Required for skill sync)")]
    [SerializeField]
    private SpawnConfigSO _spawnConfig;

    private uint _currentTick;
    private readonly Queue<ClientInputState> _inputHistory = new(60);

    private Vector3 _previousPosition;
    private Vector3 _targetPosition;
    private Quaternion _previousRotation = Quaternion.identity;
    private Quaternion _targerRotation = Quaternion.identity;
    private float _interpolationTime;
    private float _interpolationDuration = 0.1f; // Time between expected network updates
    
    // Network optimization: velocity-based dead reckoning
    private Vector3 _estimatedVelocity;
    private Vector3 _smoothedVelocity; // Dampened velocity for smoother extrapolation
    private float _lastNetworkUpdateTime;
    private uint _lastReceivedTick;
    
    // Constants for interpolation tuning
    private const float SNAP_DISTANCE_THRESHOLD = 5f; // Snap if too far (teleport)
    private const float VELOCITY_SMOOTHING = 0.3f; // How much to smooth velocity changes
    private const float MAX_EXTRAPOLATION_TIME = 0.15f; // Max time to extrapolate beyond target
    
    // OPTIMIZATION: Cache last synced animation values to throttle RPCs
    // Only broadcasts when values actually change
    private Dictionary<string, float> _lastSyncedAnimValues = new();
    
    // OPTIMIZATION: Cache last synced movement state to throttle NetworkVariable updates
    private NetworkMovementState _lastSyncedMovement;

    /////////////////////////////////////////////////////////////////////////////

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        World localWorld = WorldRunner.Instance?.World;
        if (localWorld == null)
        {
            Debug.LogError("[NetworkSyncView] WorldRunner.Instance.World is null!");
            return;
        }

        // Server: Entity should already be initialized by CharacterFactory
        if (IsServer)
        {
            if (!_isServerInitialized)
            {
                Debug.LogError(
                    $"[NetworkSyncView] Server entity not initialized! CharacterFactory must call Initialize() before spawning. NetworkObjectId: {NetworkObjectId}"
                );
            }
            else if (_world == null || _entity.Equals(default))
            {
                Debug.LogError(
                    $"[NetworkSyncView] Server initialized but _world or _entity is null! This should never happen. NetworkObjectId: {NetworkObjectId}"
                );
                Debug.LogError(
                    $"[NetworkSyncView] _world is null: {_world == null}, _entity is default: {_entity.Equals(default)}"
                );
            }
            else
            {
                Debug.Log(
                    $"[NetworkSyncView] Server entity {_entity.Id} ready for client {OwnerClientId}, NetworkObjectId: {NetworkObjectId}"
                );
            }
            return;
        }

        // Client: Create local entity for this networked character
        if (IsClient)
        {
            Debug.Log(
                $"[NetworkSyncView] Client creating entity for networked character (Owner: {OwnerClientId}, IsOwner: {IsOwner}, NetworkObjectId: {NetworkObjectId})"
            );
            CreateClientEntity(localWorld);
        }
    }

    /// <summary>
    /// Creates a local entity in this client's World for a networked character
    /// Handles both local player (IsOwner) and remote players
    /// </summary>
    private void CreateClientEntity(World world)
    {
        EntityId clientEntity = world.CreateEntity();
        _entity = clientEntity;
        _world = world;

        bool isLocalPlayer = IsOwner;
        Debug.Log(
            $"[NetworkSyncView] Created client entity {clientEntity.Id} for {(isLocalPlayer ? "LOCAL" : "REMOTE")} player (ClientId: {OwnerClientId})"
        );

        // Add NetworkOwnerComponent BEFORE binding views
        // This ensures LookAtMouseView.CheckIfLocalPlayer() can find the component
        world.Components.Add(
            clientEntity,
            new NetworkOwnerComponent { ClientId = OwnerClientId, IsLocalPlayer = isLocalPlayer }
        );

        world.Components.Add(clientEntity, new NetworkSyncComponent { SyncView = this });

        NetworkObject netObj = GetComponent<NetworkObject>();
        if (netObj != null)
        {
            world.Components.Add(clientEntity, new NetworkObjectComponent { NetworkObject = netObj });
        }

        // Bind all child views AFTER adding core components
        foreach (var view in GetComponentsInChildren<EntityView>(includeInactive: true))
        {
            if (view is NetworkSyncView)
                continue;
            view.Bind(world, clientEntity);
            var registry = world.Services.Resolve<EntityViewRegistry>();
            registry.Register(view);
        }

        // This ensures client spawns at correct position from the start
        world.Components.Add(clientEntity, new TransformComponent(transform.position, transform.rotation));

        world.Components.Add(
            clientEntity,
            new MovementDataComponent
            {
                MoveSpeed = 0.1f,
                ForwardMultiplier = 1f,
                IsPlayerControlled = isLocalPlayer,
            }
        );
        world.Components.Add(clientEntity, new AnimationDataComponent());
        world.Components.Add(clientEntity, new ActionFlagComponent());
        world.Components.Add(clientEntity, new PlayerTagComponent());
        world.Components.Add(clientEntity, new HealthDataComponent { MaxHealth = 100, CurrentHealth = 100 });
        world.Components.Add(clientEntity, new CombatStateComponent());
        world.Components.Add(clientEntity, new AttackDataComponent { IsPlayerControlled = isLocalPlayer });

        // Use serialized weapon fields configured on the character prefab
        world.Components.Add(
            clientEntity,
            new WeaponDataComponent
            {
                ExecutionType = _executionType,
                ProjectilePrefab = _projectilePrefab,
                HitImpactParticlePrefab = _hitImpactParticlePrefab,
                ProjectileSpeed = _projectileSpeed,
                ProjectileLifetime = _projectileLifetime,
                ProjectileSpawnOffset = _projectileSpawnOffset,
                AttackAnimationTrigger = _attackAnimationTrigger,
                TotalAttackAnimations = _totalAttackAnimations,
                BaseDamage = _baseDamage,
                BaseCooldown = _baseCooldown,
                BaseRange = _baseRange,
            }
        );
        world.Components.Add(
            clientEntity,
            new SkillSetComponent(new System.Collections.Generic.List<SkillDefinitionSO>())
        );
        world.Components.Add(clientEntity, new SkillCastBufferComponent());
        world.Components.Add(clientEntity, new AudioProfileComponent());
        world.Components.Add(clientEntity, new CharacterSelectionComponent());

        // Add game state components
        world.Components.Add(clientEntity, new PlayerScoreComponent());
        world.Components.Add(clientEntity, new PlayerRespawnComponent { OriginalSpawnPosition = transform.position });

        // Subscribe to NetworkVariable changes on client
        _netHealth.OnValueChanged += OnNetHealthChanged;
        _netScore.OnValueChanged += OnNetScoreChanged;
        world.Components.Add(clientEntity, new PlayerUpgradesComponent());

        Debug.Log($"[NetworkSyncView] Client entity {clientEntity.Id} components added, requesting character data...");

        // Initialize interpolation variables for remote players (spectators or non-owners)
        // This prevents the character from appearing at (0,0,0) before the first network update
        if (!isLocalPlayer)
        {
            _previousPosition = transform.position;
            _targetPosition = transform.position;
            _previousRotation = transform.rotation;
            _targerRotation = transform.rotation;
            _interpolationTime = _interpolationDuration; // Start at "completed" so we use current position
            _lastNetworkUpdateTime = Time.time;
            
            Debug.Log($"[NetworkSyncView] Initialized interpolation for remote player at {transform.position}");
        }

        // Request character data from server BEFORE publishing spawn event
        // This ensures camera and other systems get correct data
        if (!IsServer)
        {
            RequestCharacterDataServerRpc();
        }
    }

    public void Initialize(World world, EntityId entity)
    {
        // This method should only be called on the server by CharacterFactory
        // BEFORE the NetworkObject is spawned
        if (!NetworkManager.Singleton.IsServer)
        {
            Debug.LogWarning("[NetworkSyncView] Initialize() called but NetworkManager is not server!");
            return;
        }

        if (_isServerInitialized)
        {
            Debug.LogWarning($"[NetworkSyncView] Initialize() called multiple times! Entity {entity.Id}");
            return;
        }

        _world = world;
        _entity = entity;
        _isServerInitialized = true;

        if (_world == null)
        {
            Debug.LogError("[NetworkSyncView] Initialize called with null World!");
            _isServerInitialized = false;
            return;
        }

        if (_entity.Equals(default))
        {
            Debug.LogError("[NetworkSyncView] Initialize called with invalid EntityId!");
            _isServerInitialized = false;
            return;
        }

        // Server event subscriptions
        _world.Events.Subscribe<HealthChangedEvent>(OnHealthChanged);
        _world.Events.Subscribe<CombatStateChangedEvent>(OnCombatStateChanged);
        _world.Events.Subscribe<AnimationParameterEvent>(OnAnimationParameter);
        _world.Events.Subscribe<AttackExecutionRequestEvent>(OnAttackExecutionRequest);

        Debug.Log(
            $"[NetworkSyncView] Server initialized entity {entity.Id}, _isServerInitialized: {_isServerInitialized}"
        );
    }

    private void Start()
    {
        respawnUI = FindFirstObjectByType<PlayerRespawnUI>();

        if (IsClient && !IsServer)
        {
            try
            {
                _netTransform.OnValueChanged += OnNetTransformChanged;
                _netHealth.OnValueChanged += OnNetHealthChanged;
                _netCombatState.OnValueChanged += OnNetCombatStateChanged;
                _netMovement.OnValueChanged += OnNetMovementChanged;
                Debug.Log($"[NetworkSyncView] Client subscribed to NetworkVariable changes for entity {_entity.Id}");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[NetworkSyncView] Failed to subscribe to NetworkVariables on client: {ex.Message}");
            }
        }
    }

    private void Update()
    {
        if (_world == null || _entity.Equals(default))
        {
            return;
        }

        if (IsServer)
        {
            ServerUpdate();
        }
        else if (IsOwner)
        {
            ClientPredictionUpdate();
        }
        else
        {
            ClientInterpolation();
        }
    }

    private void FixedUpdate()
    {
        _currentTick++;
    }

    public override void OnNetworkDespawn()
    {
        if (_world != null && !_entity.Equals(default))
        {
            try
            {
                _world.DestroyEntity(_entity);
                Debug.Log($"[NetworkSyncView] Destroyed entity {_entity.Id} on despawn");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[NetworkSyncView] Error destroying entity on despawn: {ex.Message}");
            }
        }

        if (IsServer)
        {
            _world?.Events.Unsubscribe<HealthChangedEvent>(OnHealthChanged);
            _world?.Events.Unsubscribe<CombatStateChangedEvent>(OnCombatStateChanged);
            _world?.Events.Unsubscribe<AnimationParameterEvent>(OnAnimationParameter);
            _world?.Events.Unsubscribe<AttackExecutionRequestEvent>(OnAttackExecutionRequest);
        }

        if (IsClient && !IsServer)
        {
            _netTransform.OnValueChanged -= OnNetTransformChanged;
            _netHealth.OnValueChanged -= OnNetHealthChanged;
            _netCombatState.OnValueChanged -= OnNetCombatStateChanged;
            _netMovement.OnValueChanged -= OnNetMovementChanged;
        }
    }

    ////////////////////////////////////////////////////////////////

    #region SERVER Method

    private void ServerUpdate()
    {
        SyncTransform();
        SyncMovement();
        SyncScore();
    }

    private void SyncScore()
    {
        // OPTIMIZATION: Score rarely changes, sync every 10 ticks (~6Hz)
        if (_currentTick % 10 != 0) return;
        
        if (_world.Components.TryGet(_entity, out PlayerScoreComponent score))
        {
            var newState = new NetworkScoreState
            {
                TotalScore = score.TotalScore,
                PlayerKills = score.PlayerKills,
                EnemyKills = score.EnemyKills,
            };

            // Only update if changed
            if (_netScore.Value.TotalScore != newState.TotalScore ||
                _netScore.Value.PlayerKills != newState.PlayerKills ||
                _netScore.Value.EnemyKills != newState.EnemyKills)
            {
                _netScore.Value = newState;
            }
        }
    }

    private void SyncTransform()
    {
        if (_world.Components.TryGet(_entity, out TransformComponent trans))
        {
            var newState = new NetworkTransformState
            {
                Position = trans.Position,
                Rotation = trans.Rotation,
                Tick = _currentTick,
            };

            // OPTIMIZATION: Increased thresholds to reduce sync frequency
            // 0.05f position (was 0.01f), 3f rotation (was 1f)
            if (
                Vector3.Distance(_netTransform.Value.Position, newState.Position) > 0.05f
                || Quaternion.Angle(_netTransform.Value.Rotation, newState.Rotation) > 3f
            )
            {
                _netTransform.Value = newState;
            }
        }
    }

    private void SyncMovement()
    {
        if (_currentTick % 2 == 0)
        {
            if (_world.Components.TryGet(_entity, out MovementDataComponent movement))
            {
                var newState = new NetworkMovementState
                {
                    MoveDirection = movement.MoveDirection,
                    IsMoving = movement.IsMoving,
                    IsGrounded = movement.IsGrounded,
                    IsStunned = movement.IsStunned,
                };
                
                // OPTIMIZATION: Only sync if values actually changed
                bool changed = 
                    newState.IsMoving != _lastSyncedMovement.IsMoving ||
                    newState.IsGrounded != _lastSyncedMovement.IsGrounded ||
                    newState.IsStunned != _lastSyncedMovement.IsStunned ||
                    (newState.IsMoving && Vector3.SqrMagnitude(newState.MoveDirection - _lastSyncedMovement.MoveDirection) > 0.01f);
                    
                if (changed)
                {
                    _netMovement.Value = newState;
                    _lastSyncedMovement = newState;
                }
            }
        }
    }

    #endregion

    /////////////////////////////////////////////////////////////////////////////

    #region CLIENT Method

    private void ClientPredictionUpdate()
    {
        if (_world == null)
        {
            return;
        }

        if (!_world.Components.TryGet(_entity, out NetworkOwnerComponent owner))
        {
            return;
        }

        if (!owner.IsLocalPlayer)
        {
            return;
        }

        var inputService = _world.Services.Resolve<IInputService>();
        if (inputService == null)
        {
            return;
        }

        // Get current movement input
        Vector2 moveInput = inputService.GetMoveInput();

        // Create input state for server
        var inputState = new ClientInputState
        {
            Tick = _currentTick,
            MoveInput = moveInput,
            MouseWorldPos = inputService.GetMouseWorldPosition(),
        };

        // Store for client-side prediction/reconciliation
        _inputHistory.Enqueue(inputState);
        if (_inputHistory.Count > 60)
        {
            _inputHistory.Dequeue();
        }

        // OPTIMIZATION: Send input every 2 ticks (~30Hz) instead of every frame
        // This reduces bandwidth by 50% while still maintaining smooth movement
        if (_currentTick % 2 == 0)
        {
            SendInputToServerRpc(inputState);
        }

        // Update local movement component for immediate client-side prediction
        if (_world.Components.TryGet(_entity, out MovementDataComponent movement))
        {
            movement.InputDirection = moveInput;
        }
    }

    private void ClientInterpolation()
    {
        if (_world == null || IsOwner || IsServer)
        {
            return;
        }

        // Advance interpolation time
        _interpolationTime += Time.deltaTime;
        
        // Calculate interpolation progress (0 to 1+)
        float t = _interpolationDuration > 0.001f ? _interpolationTime / _interpolationDuration : 1f;
        
        if (!_world.Components.TryGet(_entity, out TransformComponent trans))
            return;

        if (t < 1f)
        {
            // Standard interpolation toward target
            trans.Position = Vector3.Lerp(_previousPosition, _targetPosition, t);
        }
        else
        {
            // DEAD RECKONING: Extrapolate beyond target using smoothed velocity
            float extrapolationTime = Mathf.Min(_interpolationTime - _interpolationDuration, MAX_EXTRAPOLATION_TIME);
            
            // Use smoothed velocity with gradual dampening to prevent overshoot
            float damping = 1f - Mathf.Clamp01(extrapolationTime / MAX_EXTRAPOLATION_TIME);
            trans.Position = _targetPosition + _smoothedVelocity * extrapolationTime * damping;
        }
        
        // Smooth rotation interpolation with validation
        float rotT = Mathf.Clamp01(t * 1.2f); // Rotation completes slightly faster
        
        bool previousValid =
            _previousRotation != Quaternion.identity
            && !float.IsNaN(_previousRotation.x)
            && !float.IsNaN(_previousRotation.y)
            && !float.IsNaN(_previousRotation.z)
            && !float.IsNaN(_previousRotation.w);

        bool targetValid =
            _targerRotation != Quaternion.identity
            && !float.IsNaN(_targerRotation.x)
            && !float.IsNaN(_targerRotation.y)
            && !float.IsNaN(_targerRotation.z)
            && !float.IsNaN(_targerRotation.w);

        if (previousValid && targetValid)
        {
            trans.Rotation = Quaternion.Slerp(_previousRotation, _targerRotation, rotT);
        }
        else if (targetValid)
        {
            trans.Rotation = _targerRotation;
        }
        else if (previousValid)
        {
            trans.Rotation = _previousRotation;
        }
        else
        {
            trans.Rotation = Quaternion.identity;
        }
    }

    #endregion

    ///////////////////////////////////////////////////////////////////////

    #region RPC

    // INPUT

    [ServerRpc]
    private void SendInputToServerRpc(ClientInputState input)
    {
        // Null check to prevent NullReferenceException
        if (_world == null || _entity.Equals(default))
        {
            Debug.LogError($"[NetworkSyncView] SendInputToServerRpc called but _world or _entity is null!");
            return;
        }

        // Apply rotation from mouse position FIRST
        // This ensures movement direction (transform.right/forward) matches client's view
        if (_world.Components.TryGet(_entity, out TransformComponent trans))
        {
            Vector3 aimDir = (input.MouseWorldPos - trans.Position).normalized;
            aimDir.y = 0;

            if (aimDir.sqrMagnitude > 0.01f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(aimDir);
                trans.Rotation = targetRotation;

                transform.rotation = targetRotation;
            }
        }

        // Update server's movement component with client input
        if (_world.Components.TryGet(_entity, out MovementDataComponent movement))
        {
            movement.InputDirection = input.MoveInput;

            // Also publish to server's EventBus so MovementSystem can process it
            // Without this, the server's MovementSystem won't calculate MoveDirection
            if (input.MoveInput.sqrMagnitude > 0.01f)
            {
                _world.Events.Publish(new MovePressedInputEvent(_entity, input.MoveInput));
            }
        }

        if (_world.Components.TryGet(_entity, out NetworkSyncComponent sync))
        {
            sync.LastProcessedInputTick = input.Tick;
        }

        AcknowledgeInputClientRpc(input.Tick);
    }

    [ClientRpc]
    private void AcknowledgeInputClientRpc(uint acknowledgedTick)
    {
        if (!IsOwner)
        {
            return;
        }

        while (_inputHistory.Count > 0 && _inputHistory.Peek().Tick <= acknowledgedTick)
        {
            _inputHistory.Dequeue();
        }

        // Reconciliation
        if (_world.Components.TryGet(_entity, out TransformComponent trans))
        {
            float distance = Vector3.Distance(trans.Position, _netTransform.Value.Position);

            if (distance > 0.5f)
            {
                trans.Position = _netTransform.Value.Position;
                trans.Rotation = _netTransform.Value.Rotation;

                // Sync to Unity Transform
                var registry = _world.Services.Resolve<EntityViewRegistry>();
                if (registry.TryGet(_entity, out EntityView view))
                {
                    view.transform.position = trans.Position;
                    view.transform.rotation = trans.Rotation;
                }
            }
        }
    }

    // ----------------------------------------------------------------------

    // ATTACK

    [ServerRpc]
    public void RequestAttackServerRpc(Vector3 mouseWorldPos)
    {
        Debug.Log($"[NetworkSyncView] RequestAttackServerRpc received from client, mousePos: {mouseWorldPos}");

        if (_world == null || _entity.Equals(default))
        {
            Debug.LogError($"[NetworkSyncView] RequestAttackServerRpc called but _world or _entity is null!");
            return;
        }

        if (!_world.Components.TryGet(_entity, out AttackDataComponent attack))
        {
            Debug.LogWarning(
                $"[NetworkSyncView] RequestAttackServerRpc: Entity {_entity.Id} missing AttackDataComponent"
            );
            RejectAttackClientRpc();
            return;
        }

        if (!_world.Components.TryGet(_entity, out WeaponDataComponent weapon))
        {
            Debug.LogWarning(
                $"[NetworkSyncView] RequestAttackServerRpc: Entity {_entity.Id} missing WeaponDataComponent"
            );
            RejectAttackClientRpc();
            return;
        }

        if (!attack.CanAttack(weapon.BaseCooldown) || attack.IsAttacking)
        {
            Debug.Log(
                $"[NetworkSyncView] RequestAttackServerRpc: Attack rejected - CanAttack: {attack.CanAttack(weapon.BaseCooldown)}, IsAttacking: {attack.IsAttacking}"
            );
            RejectAttackClientRpc();
            return;
        }

        // Capture the aim direction from the client's mouse position
        attack.AttackDirection = CalculateAttackDirection(mouseWorldPos);
        // NOTE: LastAttackTime is set in AttackSystem.OnAttackRequest, not here
        // Setting it here would cause CanAttack() to fail in AttackSystem

        Debug.Log(
            $"[NetworkSyncView] RequestAttackServerRpc: Publishing AttackPressedInputEvent for entity {_entity.Id}"
        );
        _world.Events.Publish(new AttackPressedInputEvent(_entity));

        BroadcastAttackClientRpc();
    }

    [ClientRpc]
    private void BroadcastAttackClientRpc()
    {
        // Owner predict the attack
        if (IsOwner)
        {
            return;
        }

        _world.Events.Publish(new AttackPressedInputEvent(_entity));
    }

    [ClientRpc]
    private void RejectAttackClientRpc()
    {
        if (!IsOwner)
        {
            return;
        }

        if (_world.Components.TryGet(_entity, out AttackDataComponent attack))
        {
            attack.IsAttacking = false;
        }

        if (_world.Components.TryGet(_entity, out CombatStateComponent state))
        {
            state.CurrentState = CombatState.Idle;
        }
    }

    [ClientRpc]
    private void BroadcastAttackExecutionClientRpc(
        AttackExecutionType type,
        Vector3 direction,
        float range,
        float damage,
        float projectileSpeed,
        float projectileLifetime,
        Vector3 spawnOffset
    )
    {
        Debug.Log(
            $"[NetworkSyncView] BroadcastAttackExecutionClientRpc received for entity {_entity.Id}, Type: {type}"
        );

        if (IsServer)
        {
            return;
        }

        if (_world == null || _entity.Equals(default))
        {
            Debug.LogWarning($"[NetworkSyncView] BroadcastAttackExecutionClientRpc: World or entity not initialized");
            return;
        }

        // Get weapon data for animation and projectile prefab
        if (!_world.Components.TryGet(_entity, out WeaponDataComponent weapon))
        {
            Debug.LogWarning(
                $"[NetworkSyncView] BroadcastAttackExecutionClientRpc: No WeaponDataComponent for entity {_entity.Id}"
            );
            return;
        }

        // ANIMATION - Only for NON-OWNER clients
        // Owner already predicted and played animation locally, so skip to avoid double-trigger/reset
        if (!IsOwner)
        {
            int randomIndex = UnityEngine.Random.Range(0, weapon.TotalAttackAnimations);

            _world.Events.Publish(
                new AnimationParameterEvent(_entity, "attackIndex", AnimationParameterType.Float, randomIndex)
            );

            _world.Events.Publish(
                new AnimationParameterEvent(
                    _entity,
                    weapon.AttackAnimationTrigger,
                    AnimationParameterType.Trigger,
                    null
                )
            );
        }

        // PROJECTILE - Spawn visual-only projectile on ALL clients
        if (type == AttackExecutionType.Projectile && weapon.ProjectilePrefab != null)
        {
            SpawnClientProjectile(direction, projectileSpeed, projectileLifetime, spawnOffset, weapon);
        }
    }

    /// <summary>
    /// Spawns a visual-only projectile on the client (no damage, just movement and effects)
    /// </summary>
    private void SpawnClientProjectile(
        Vector3 direction,
        float speed,
        float lifetime,
        Vector3 spawnOffset,
        WeaponDataComponent weapon
    )
    {
        var registry = _world.Services.Resolve<EntityViewRegistry>();
        if (!registry.TryGet(_entity, out EntityView attackerView))
        {
            return;
        }

        Transform attackerTf = attackerView.transform;

        // Calculate spawn position
        Vector3 forwardDir = direction.sqrMagnitude < 0.0001f ? attackerTf.forward : direction.normalized;
        forwardDir.y = 0f;
        forwardDir = forwardDir.normalized;

        // Try to find ProjectileSpawnPos component
        Transform spawnTransform = attackerTf;
        ProjectileSpawnPos spawnPosComponent = attackerTf.GetComponentInChildren<ProjectileSpawnPos>();

        Vector3 spawnPos;
        if (spawnPosComponent != null)
        {
            spawnTransform = spawnPosComponent.transform;
            spawnPos = spawnTransform.position + spawnTransform.TransformDirection(spawnOffset);
        }
        else
        {
            spawnPos = attackerTf.position + new Vector3(0f, 1.3f, 0f);
            if (spawnOffset.sqrMagnitude > 0.0001f)
            {
                spawnPos += Quaternion.LookRotation(forwardDir, Vector3.up) * spawnOffset;
            }
        }

        Quaternion spawnRot = Quaternion.LookRotation(forwardDir, Vector3.up);

        // Get or create projectile from pool
        var pool = _world.Services.Resolve<ObjectPoolService>();
        if (pool == null || weapon.ProjectilePrefab == null)
        {
            return;
        }

        GameObject projectileGO = pool.Get(weapon.ProjectilePrefab, spawnPos, spawnRot);

        if (!projectileGO.TryGetComponent(out ProjectileView projectile))
        {
            projectile = projectileGO.AddComponent<ProjectileView>();
        }

        // Initialize with 0 damage - visual only on client!
        projectile.Initialize(
            _world,
            _entity,
            0f, // NO DAMAGE on client - server handles damage
            speed,
            lifetime,
            forwardDir,
            weapon.HitImpactParticlePrefab,
            weapon.ProjectilePrefab,
            spawnPos,
            spawnRot
        );
    }

    [ClientRpc]
    public void BroadcastDamageVisualClientRpc(float amount, Vector3 hitPoint)
    {
        if (IsServer)
        {
            return;
        }

        Debug.Log($"Client received damage visual: {amount} at {hitPoint}");
    }

    /// <summary>
    /// Broadcasts melee hit impact effects to all clients
    /// </summary>
    [ClientRpc]
    public void BroadcastMeleeHitClientRpc(Vector3 hitPoint, Vector3 hitNormal)
    {
        if (IsServer)
        {
            return;
        }

        // Spawn melee impact effect on client
        if (!_world.Components.TryGet(_entity, out WeaponDataComponent weapon))
        {
            return;
        }

        if (weapon.HitImpactParticlePrefab != null)
        {
            var rotation = hitNormal.sqrMagnitude > 0.001f ? Quaternion.LookRotation(hitNormal) : Quaternion.identity;
            var impactGo = Instantiate(weapon.HitImpactParticlePrefab.gameObject, hitPoint, rotation);
            Destroy(impactGo, 2f);
        }
    }

    /// <summary>
    /// Broadcasts skill hit VFX to all clients.
    /// Used for melee skills that have hit detection on server.
    /// </summary>
    [ClientRpc]
    public void BroadcastSkillHitVfxClientRpc(Vector3 hitPoint, int skillIndex)
    {
        if (IsServer)
        {
            return;
        }

        // Get skill from entity's skill set
        if (!_world.Components.TryGet(_entity, out SkillSetComponent skillSet))
        {
            return;
        }

        if (skillIndex < 0 || skillIndex >= skillSet.Skills.Count)
        {
            return;
        }

        var skill = skillSet.Skills[skillIndex];

        // Try to get hit VFX from different skill types
        ParticleSystem hitVfxPrefab = null;

        if (skill is HomerunSwingSkillSO homerunSkill && homerunSkill.hitVfxPrefab != null)
        {
            hitVfxPrefab = homerunSkill.hitVfxPrefab;
        }
        else if (skill is DashStrikeSkillSO dashSkill && dashSkill.hitVfxPrefab != null)
        {
            hitVfxPrefab = dashSkill.hitVfxPrefab;
        }

        if (hitVfxPrefab != null)
        {
            var hitVfx = Instantiate(hitVfxPrefab, hitPoint, Quaternion.identity);
            Destroy(hitVfx, 2f);
        }
    }

    // -----------------------------------------------------------

    //  SKIlLS

    [ServerRpc]
    public void RequestSkillServerRpc(int skillIndex, bool isPressed, Vector3 mousePos)
    {
        if (!_world.Components.TryGet(_entity, out SkillSetComponent skillSet))
        {
            return;
        }

        int index = skillIndex - 1;

        if (index < 0 || index >= skillSet.Skills.Count)
        {
            return;
        }

        if (Time.time < skillSet.CooldownUntil[index])
        {
            return;
        }

        // Ensure server has a buffer to store the chosen skill (authoritative selection)
        if (!_world.Components.TryGet(_entity, out SkillCastBufferComponent buffer))
        {
            buffer = new SkillCastBufferComponent();
            _world.Components.Add(_entity, buffer);
        }

        // When the key is pressed, prime the buffer with the selected skill
        if (isPressed)
        {
            buffer.Skill = skillSet.Skills[index];

            // Seed target point toward the mouse for better defaults if execution RPC arrives with bad data
            buffer.TargetPoint = mousePos;

            // Derive a rough direction toward mouse using current transform as a fallback
            Vector3 dir = mousePos;
            if (_world.Components.TryGet(_entity, out TransformComponent trans))
            {
                dir -= trans.Position;
            }
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f)
            {
                dir = Vector3.forward;
            }
            buffer.Direction = dir.normalized;
        }
        else
        {
            // DON'T clear buffer.Skill here!
            // The skill execution is triggered by mouse click (RequestSkillExecutionServerRpc),
            // which may arrive AFTER the key is released. Clearing here causes a race condition
            // where releasing the key before the execution RPC arrives cancels the skill.
            // The buffer is cleared in HandleSkillHit after successful execution.
        }

        _world.Events.Publish(new SkillPressedInputEvent(_entity, skillIndex, isPressed));

        BroadcastSkillClientRpc(skillIndex, isPressed, mousePos);
    }

    [ClientRpc]
    private void BroadcastSkillClientRpc(int skillIndex, bool isPressed, Vector3 mousePos)
    {
        if (IsOwner)
        {
            return;
        }

        _world.Events.Publish(new SkillPressedInputEvent(_entity, skillIndex, isPressed));
    }

    [ServerRpc]
    public void RequestSkillExecutionServerRpc(Vector3 targetPoint, Vector3 direction, int skillIndex = -1)
    {
        Debug.Log(
            $"[NetworkSyncView] RequestSkillExecutionServerRpc received for entity {_entity.Id}, target: {targetPoint}, direction: {direction}, skillIndex: {skillIndex}"
        );

        if (!_world.Components.TryGet(_entity, out SkillCastBufferComponent buffer))
        {
            buffer = new SkillCastBufferComponent();
            _world.Components.Add(_entity, buffer);
        }

        // For instant skills, the buffer might not be set yet due to RPC race condition
        // Use the skillIndex to set it directly
        if (buffer.Skill == null && skillIndex >= 0)
        {
            if (_world.Components.TryGet(_entity, out SkillSetComponent skillSetBuffer))
            {
                if (skillIndex < skillSetBuffer.Skills.Count)
                {
                    buffer.Skill = skillSetBuffer.Skills[skillIndex];
                    buffer.TargetPoint = targetPoint;
                    buffer.Direction = direction;
                    Debug.Log(
                        $"[NetworkSyncView] Set buffer.Skill from skillIndex {skillIndex}: {buffer.Skill?.skillName}"
                    );
                }
            }
        }

        if (buffer.Skill == null)
        {
            Debug.LogWarning(
                $"[NetworkSyncView] RequestSkillExecutionServerRpc FAILED - buffer.Skill is null for entity {_entity.Id}"
            );
            return;
        }

        Debug.Log($"[NetworkSyncView] Processing skill execution for {buffer.Skill.skillName}");

        if (_world.Components.TryGet(_entity, out SkillSetComponent skillSet))
        {
            int index = -1;

            for (int i = 0; i < skillSet.Skills.Count; i++)
            {
                if (skillSet.Skills[i] == buffer.Skill)
                {
                    index = i;
                    break;
                }
            }

            if (index != -1 && Time.time < skillSet.CooldownUntil[index])
            {
                return;
            }
        }

        // Validate and persist direction for downstream systems
        Vector3 validatedDirection = direction;
        validatedDirection.y = 0f;

        if (validatedDirection.sqrMagnitude < 0.0001f)
        {
            if (_world.Components.TryGet(_entity, out TransformComponent trans))
            {
                validatedDirection = trans.Rotation * Vector3.forward;
            }
            else
            {
                var registry = _world.Services.Resolve<EntityViewRegistry>();
                if (registry.TryGet(_entity, out EntityView view))
                {
                    validatedDirection = view.transform.forward;
                }
            }
        }

        if (validatedDirection.sqrMagnitude < 0.0001f)
        {
            validatedDirection = Vector3.forward;
        }

        validatedDirection = validatedDirection.normalized;
        buffer.TargetPoint = targetPoint;
        buffer.Direction = validatedDirection;

        // Simulate on server
        _world.Events.Publish(new EnterCombatStateEvent { Entity = _entity, TargetState = CombatState.CastingSkill });

        _world.Events.Publish(
            new SkillEffectTriggerEvent
            {
                Caster = _entity,
                Skill = buffer.Skill,
                TargetPoint = targetPoint,
                Direction = validatedDirection,
            }
        );

        // Publish SkillConfirmExecutionEvent so skill executors actually execute
        // This is what ExplosiveShotExecutorView, SniperShotExecutorView, etc. listen for
        _world.Events.Publish(new SkillConfirmExecutionEvent(_entity, buffer.Skill, targetPoint, validatedDirection));

        // Find the skill index to pass to clients so they can look up the skill and apply cooldown
        int skillIndexToSend = -1;
        if (_world.Components.TryGet(_entity, out SkillSetComponent skillSetForIndex))
        {
            for (int i = 0; i < skillSetForIndex.Skills.Count; i++)
            {
                if (skillSetForIndex.Skills[i] == buffer.Skill)
                {
                    skillIndexToSend = i;
                    break;
                }
            }
        }
        
        BroadcastSkillExecutionClientRpc(targetPoint, validatedDirection, skillIndexToSend);
    }

    [ClientRpc]
    private void BroadcastSkillExecutionClientRpc(Vector3 targetPoint, Vector3 direction, int skillIndex)
    {
        Debug.Log(
            $"[NetworkSyncView] BroadcastSkillExecutionClientRpc called, Entity: {_entity.Id}, IsServer: {IsServer}, skillIndex: {skillIndex}"
        );

        // Server already processed this in the RPC caller context
        if (IsServer)
        {
            return;
        }

        // Look up skill from SkillSetComponent using the index
        // This is more reliable than using buffer.Skill which may not be set on client for instant skills
        SkillDefinitionSO skill = null;
        
        if (skillIndex >= 0 && _world.Components.TryGet(_entity, out SkillSetComponent skillSet))
        {
            if (skillIndex < skillSet.Skills.Count)
            {
                skill = skillSet.Skills[skillIndex];
            }
        }
        
        // Fallback to buffer.Skill if index lookup fails
        if (skill == null)
        {
            if (_world.Components.TryGet(_entity, out SkillCastBufferComponent buffer) && buffer.Skill != null)
            {
                skill = buffer.Skill;
            }
        }

        if (skill == null)
        {
            Debug.LogWarning(
                $"[NetworkSyncView] BroadcastSkillExecutionClientRpc: Could not find skill for entity {_entity.Id} (skillIndex: {skillIndex})"
            );
            return;
        }

        Debug.Log(
            $"[NetworkSyncView] Publishing SkillEffectTriggerEvent for skill: {skill.skillName}, category: {skill.category}"
        );

        _world.Events.Publish(
            new SkillEffectTriggerEvent
            {
                Caster = _entity,
                Skill = skill,
                TargetPoint = targetPoint,
                Direction = direction,
            }
        );
    }

    /// <summary>
    /// Broadcasts instant skill execution from server to all clients.
    /// This is specifically for skills with isInstant=true that are executed directly on the server
    /// (like PlasmaShield when host uses it) - these bypass the normal RequestSkillExecutionServerRpc path.
    /// </summary>
    [ClientRpc]
    public void BroadcastInstantSkillClientRpc(Vector3 targetPoint, Vector3 direction, int skillIndex)
    {
        // Server already handled this locally
        if (IsServer)
        {
            return;
        }
        
        // Skip if this is the owner - they already played prediction locally
        if (IsOwner)
        {
            return;
        }

        Debug.Log($"[NetworkSyncView] BroadcastInstantSkillClientRpc received, Entity: {_entity.Id}, skillIndex: {skillIndex}");

        if (_world == null || _entity.Equals(default))
        {
            Debug.LogWarning("[NetworkSyncView] BroadcastInstantSkillClientRpc: World or entity not initialized");
            return;
        }

        // Look up skill from entity's skill set
        SkillDefinitionSO skill = null;
        if (_world.Components.TryGet(_entity, out SkillSetComponent skillSet))
        {
            if (skillIndex >= 0 && skillIndex < skillSet.Skills.Count)
            {
                skill = skillSet.Skills[skillIndex];
            }
        }

        if (skill == null)
        {
            Debug.LogWarning($"[NetworkSyncView] BroadcastInstantSkillClientRpc: Could not find skill for index {skillIndex}");
            return;
        }

        Debug.Log($"[NetworkSyncView] Publishing instant skill effect for {skill.skillName}, category: {skill.category}");

        // Publish the SkillEffectTriggerEvent so clients (including spectators) play the VFX
        _world.Events.Publish(
            new SkillEffectTriggerEvent
            {
                Caster = _entity,
                Skill = skill,
                TargetPoint = targetPoint,
                Direction = direction,
            }
        );
    }

    [ClientRpc]
    public void BroadcastSkillEffectClientRpc(SkillCategory category, Vector3 targetPoint, Vector3 direction)
    {
        if (IsServer)
        {
            return;
        }

        if (!_world.Components.TryGet(_entity, out SkillSetComponent skillSet))
        {
            return;
        }

        SkillDefinitionSO skill = null;

        foreach (var s in skillSet.Skills)
        {
            if (s.category == category)
            {
                skill = s;
                break;
            }
        }

        if (skill == null)
        {
            return;
        }

        _world.Events.Publish(
            new SkillEffectTriggerEvent
            {
                Caster = _entity,
                Skill = skill,
                TargetPoint = targetPoint,
                Direction = direction,
            }
        );
    }

    [ClientRpc]
    public void BroadcastKnockbackClientRpc(Vector3 direction, float force)
    {
        if (IsServer)
        {
            return;
        }

        if (_world == null || _entity.Equals(default))
        {
            Debug.LogWarning($"[NetworkSyncView] BroadcastKnockbackClientRpc: World or entity not initialized");
            return;
        }

        if (!_world.Components.TryGet(_entity, out TransformComponent transform))
        {
            return;
        }

        Vector3 knockback = direction.normalized * force;
        transform.Position += knockback * Time.deltaTime;

        var registry = _world.Services.Resolve<EntityViewRegistry>();
        if (registry != null && registry.TryGet(_entity, out EntityView view))
        {
            view.transform.position = transform.Position;
        }
    }

    [ClientRpc]
    public void BroadcastStunClientRpc(float duration)
    {
        if (IsServer)
        {
            return;
        }

        if (_world == null || _entity.Equals(default))
        {
            Debug.LogWarning($"[NetworkSyncView] BroadcastStunClientRpc: World or entity not initialized");
            return;
        }

        if (_world.Components.TryGet(_entity, out MovementDataComponent movement))
        {
            movement.IsStunned = true;

            // Client-side stun timer
            StartCoroutine(ClientStunRoutine(duration));
        }
    }

    private System.Collections.IEnumerator ClientStunRoutine(float duration)
    {
        yield return new WaitForSeconds(duration);

        if (_world.Components.TryGet(_entity, out MovementDataComponent movement))
        {
            movement.IsStunned = false;
        }
    }

    // -----------------------------------------------------------------------

    // RESPAWN

    [ClientRpc]
    public void BroadcastRespawnTimerClientRpc(float respawnDelay)
    {
        // Only show respawn UI to the owning player
        if (!IsOwner)
        {
            return;
        }

        Debug.Log($"[Client] Player will respawn in {respawnDelay}");

        if (respawnUI == null)
        {
            Debug.LogError("respawnUI is null!");

            return;
        }

        respawnUI.ShowRespawnTimer(respawnDelay);
    }

    [ClientRpc]
    public void BroadcastPlayerRespawnClientRpc(Vector3 spawnPosition)
    {
        if (IsServer)
        {
            return;
        }

        Debug.Log($"[Client] Player respawned at {spawnPosition} (IsOwner: {IsOwner})");

        // Only show respawn UI to the owning player
        if (IsOwner)
        {
            if (respawnUI != null)
            {
                respawnUI.HideRespawnTimer();
            }
        }

        // Show the character GameObject and teleport to spawn position
        // This must run for ALL clients (including spectators) so they see the player again!
        var registry = _world?.Services.Resolve<EntityViewRegistry>();
        if (registry != null && registry.TryGet(_entity, out EntityView view))
        {
            view.gameObject.SetActive(true);
            view.transform.position = spawnPosition;
            
            // Also update the ECS transform component so interpolation works correctly
            if (_world.Components.TryGet(_entity, out TransformComponent trans))
            {
                trans.Position = spawnPosition;
            }
            
            // Reset health display component for non-owners
            if (_world.Components.TryGet(_entity, out HealthDataComponent health))
            {
                health.IsDead = false;
            }
        }
    }

    [ClientRpc]
    public void BroadcastDeathClientRpc()
    {
        if (IsServer)
        {
            return;
        }

        if (_world == null || _entity.Equals(default))
        {
            Debug.LogWarning($"[NetworkSyncView] BroadcastDeathClientRpc: World or entity not initialized");
            return;
        }

        // Trigger death visual/audio on clients
        _world.Events.Publish(new EntityDeathEvent(_entity));

        Debug.Log($"Client: Entity {_entity} died");

        // Hide the character GameObject when they die
        var registry = _world.Services.Resolve<EntityViewRegistry>();
        if (registry != null && registry.TryGet(_entity, out EntityView view))
        {
            view.gameObject.SetActive(false);
            // TODO: Play death animation, spawn death VFX before hiding
        }
    }

    [ClientRpc]
    private void SyncAnimationClientRpc(string paramsName, AnimationParameterType type, float value)
    {
        // Server already processed this event, clients just need to apply it
        if (IsServer)
        {
            return;
        }

        // Check if World and entity are properly initialized
        if (_world == null || _entity.Equals(default))
        {
            Debug.LogWarning($"[NetworkSyncView] SyncAnimationClientRpc: World or entity not initialized");
            return;
        }

        // BOTH owner AND remote clients need animation events
        // Owner needs movement animations (isMoving, moveX, moveY) since they're server-authoritative
        // Remote clients need all animations for visual sync
        object deserializeValue = DeserializeValue(type, value);
        _world.Events.Publish(new AnimationParameterEvent(_entity, paramsName, type, deserializeValue));
    }

    #endregion

    /////////////////////////////////////////////////////////////////////////////

    #region Callbacks

    private void OnNetMovementChanged(NetworkMovementState prev, NetworkMovementState current)
    {
        if (IsServer || IsOwner)
        {
            return;
        }

        if (_world.Components.TryGet(_entity, out MovementDataComponent movement))
        {
            movement.MoveDirection = current.MoveDirection;
            movement.IsMoving = current.IsMoving;
            movement.IsGrounded = current.IsGrounded;
            movement.IsStunned = current.IsStunned;
        }
    }

    private void OnNetCombatStateChanged(CombatState prev, CombatState current)
    {
        if (IsServer)
        {
            return;
        }

        if (_world.Components.TryGet(_entity, out CombatStateComponent combat))
        {
            combat.CurrentState = current;
        }
    }

    private void OnNetHealthChanged(NetworkHealthState prev, NetworkHealthState current)
    {
        if (IsServer)
        {
            return;
        }

        if (_world.Components.TryGet(_entity, out HealthDataComponent health))
        {
            health.CurrentHealth = current.Current;
            health.MaxHealth = current.Max;

            _world.Events.Publish(new HealthChangedEvent(_entity, current.Current, current.Max));
        }
    }

    private void OnNetScoreChanged(NetworkScoreState prev, NetworkScoreState current)
    {
        if (IsServer)
        {
            return;
        }

        if (_world.Components.TryGet(_entity, out PlayerScoreComponent score))
        {
            score.TotalScore = current.TotalScore;
            score.PlayerKills = current.PlayerKills;
            score.EnemyKills = current.EnemyKills;
        }
    }

    private void OnNetTransformChanged(NetworkTransformState prev, NetworkTransformState current)
    {
        // Server writes to NetworkVariable, so shouldn't respond to changes
        if (IsServer)
        {
            return;
        }

        // ALL clients (including owner) need position updates from server
        // The owner client no longer moves CharacterController locally,
        // so it MUST receive server position to display movement

        if (IsOwner)
        {
            // Owner client: directly update ECS component (no interpolation for snappy movement)
            // ONLY update POSITION - rotation is handled locally by LookAtMouseView
            if (_world != null && _world.Components.TryGet(_entity, out TransformComponent trans))
            {
                trans.Position = current.Position;
                // DON'T update rotation - LookAtMouseView handles it locally for responsive mouse look
            }
        }
        else
        {
            // Calculate time since last update for interpolation duration
            float timeSinceLastUpdate = Time.time - _lastNetworkUpdateTime;
            
            // Use tick delta if available for more accurate timing
            if (_lastReceivedTick > 0 && current.Tick > _lastReceivedTick)
            {
                float tickDelta = (current.Tick - _lastReceivedTick) * Time.fixedDeltaTime;
                _interpolationDuration = Mathf.Max(tickDelta, 0.016f); // Min 60Hz
                
                // Calculate new velocity and smooth it to reduce jitter
                Vector3 newVelocity = (current.Position - _targetPosition) / tickDelta;
                _estimatedVelocity = newVelocity;
                _smoothedVelocity = Vector3.Lerp(_smoothedVelocity, newVelocity, VELOCITY_SMOOTHING);
            }
            else
            {
                _interpolationDuration = Mathf.Max(timeSinceLastUpdate, 0.033f); // Fallback ~30Hz
            }
            
            _lastReceivedTick = current.Tick;
            _lastNetworkUpdateTime = Time.time;
            
            // Check for large position jump (teleport) - snap instead of interpolate
            float distanceToNewTarget = Vector3.Distance(_targetPosition, current.Position);
            if (distanceToNewTarget > SNAP_DISTANCE_THRESHOLD)
            {
                // Large jump - snap immediately
                _previousPosition = current.Position;
                _targetPosition = current.Position;
                _previousRotation = current.Rotation;
                _targerRotation = current.Rotation;
                _smoothedVelocity = Vector3.zero;
                _interpolationTime = _interpolationDuration;
                
                if (_world.Components.TryGet(_entity, out TransformComponent tf))
                {
                    tf.Position = current.Position;
                    tf.Rotation = current.Rotation;
                }
                return;
            }

            // SMOOTH TRANSITION: Start from current VISUAL position, not old target
            if (_world.Components.TryGet(_entity, out TransformComponent trans))
            {
                _previousPosition = trans.Position;
                _previousRotation = trans.Rotation;
            }
            else
            {
                _previousPosition = transform.position;
                _previousRotation = transform.rotation;
            }
            
            _targetPosition = current.Position;
            _targerRotation = current.Rotation;
            
            // Reset interpolation time to start new interpolation from current position
            _interpolationTime = 0f;
        }
    }

    private void OnAnimationParameter(AnimationParameterEvent @event)
    {
        if (!IsServer || @event.Entity != _entity)
        {
            return;
        }

        // OPTIMIZATION: Only sync if value actually changed
        // This prevents excessive RPCs every frame
        float newValue = SerializeValue(@event.Value);
        string key = @event.ParameterName;
        
        // Check if value changed (with small epsilon for floats)
        if (_lastSyncedAnimValues.TryGetValue(key, out float lastValue))
        {
            // For triggers, always sync (they're one-shot)
            if (@event.ParameterType == AnimationParameterType.Trigger)
            {
                // Triggers always sync
            }
            else if (Mathf.Abs(newValue - lastValue) < 0.01f)
            {
                // Value hasn't changed significantly, skip sync
                return;
            }
        }
        
        // Cache the new value
        _lastSyncedAnimValues[key] = newValue;
        
        SyncAnimationClientRpc(@event.ParameterName, @event.ParameterType, newValue);
    }

    private void OnCombatStateChanged(CombatStateChangedEvent @event)
    {
        if (!IsServer || @event.Entity != _entity)
        {
            return;
        }

        _netCombatState.Value = @event.Current;
    }

    private void OnHealthChanged(HealthChangedEvent @event)
    {
        if (!IsServer || @event.Entity != _entity)
        {
            return;
        }

        _netHealth.Value = new NetworkHealthState { Current = @event.CurrentHealth, Max = @event.MaxHealth };
    }

    private void OnAttackExecutionRequest(AttackExecutionRequestEvent @event)
    {
        if (!IsServer || @event.Attacker != _entity)
        {
            return;
        }

        Debug.Log(
            $"[NetworkSyncView] OnAttackExecutionRequest: Broadcasting attack for entity {_entity.Id}, Type: {@event.Type}"
        );

        // Broadcast attack animation + projectile spawn data to ALL clients
        BroadcastAttackExecutionClientRpc(
            @event.Type,
            @event.Direction,
            @event.Range,
            @event.Damage,
            @event.ProjectileSpeed,
            @event.ProjectileLifetime,
            @event.SpawnOffset
        );
    }

    #endregion

    //////////////////////////////////////////////////

    #region Utils

    private Vector3 CalculateAttackDirection(Vector3 mouseWorldPos)
    {
        Vector3 attackDir = Vector3.zero;
        Vector3 playerPos = Vector3.zero;

        // Use actual Unity transform position instead of ECS TransformComponent
        // TransformComponent may not be synced properly for client-owned entities on server
        var registry = _world.Services.Resolve<EntityViewRegistry>();
        if (registry.TryGet(_entity, out EntityView view))
        {
            playerPos = view.transform.position;
            attackDir = mouseWorldPos - playerPos;
        }
        else if (_world.Components.TryGet(_entity, out TransformComponent trans))
        {
            // Fallback to ECS component if view not found
            playerPos = trans.Position;
            attackDir = mouseWorldPos - trans.Position;
        }

        if (attackDir.sqrMagnitude < 0.0001f)
        {
            if (registry.TryGet(_entity, out EntityView v))
            {
                attackDir = v.transform.forward;
            }
        }

        attackDir.y = 0f;

        if (attackDir.sqrMagnitude < 0.0001f)
        {
            attackDir = Vector3.forward;
        }

        Debug.Log(
            $"[NetworkSyncView] CalculateAttackDirection: entity {_entity.Id}, playerPos: {playerPos}, mouseWorldPos: {mouseWorldPos}, attackDir: {attackDir.normalized}"
        );

        return attackDir.normalized;
    }

    private float SerializeValue(object value)
    {
        return value switch
        {
            bool b => b ? 1f : 0f,
            float f => f,
            int i => i,
            _ => 0f,
        };
    }

    private object DeserializeValue(AnimationParameterType type, float value)
    {
        return type switch
        {
            AnimationParameterType.Bool => value > 0.5f,
            AnimationParameterType.Float => value,
            AnimationParameterType.Int => (int)value,
            _ => null,
        };
    }

    #endregion

    /////////////////////////////////////////////////////////////////////////////

    #region Character Data Sync

    [ServerRpc(RequireOwnership = false)]
    private void RequestCharacterDataServerRpc()
    {
        // Null check to prevent NullReferenceException
        if (_world == null || _entity.Equals(default))
        {
            Debug.LogError($"[NetworkSyncView] SERVER RPC called but _world or _entity is null!");
            Debug.LogError($"[NetworkSyncView] IsServer: {IsServer}, _isServerInitialized: {_isServerInitialized}");
            Debug.LogError(
                $"[NetworkSyncView] _world is null: {_world == null}, _entity is default: {_entity.Equals(default)}"
            );
            Debug.LogError($"[NetworkSyncView] NetworkObjectId: {NetworkObjectId}, OwnerClientId: {OwnerClientId}");
            return;
        }

        if (!_world.Components.TryGet(_entity, out CharacterSelectionComponent charSelection))
        {
            Debug.LogWarning($"[NetworkSyncView] No CharacterSelectionComponent found for entity {_entity.Id}");
            return;
        }

        if (charSelection.CharacterData == null)
        {
            Debug.LogWarning($"[NetworkSyncView] CharacterData is null for entity {_entity.Id}");
            return;
        }

        var data = charSelection.CharacterData;
        Debug.Log($"[NetworkSyncView] SERVER sending character data for {data.characterName} to client");

        // Send comprehensive character data to client
        SyncCharacterDataClientRpc(
            data.characterName,
            data.maxHealth,
            data.moveSpeed,
            data.forwardMultiplier,
            data.isMovingParam,
            data.moveXParam,
            data.moveYParam,
            transform.position, // Send actual spawn position
            transform.rotation // Send actual spawn rotation
        );
        // Note: Weapon data comes from prefab's serialized fields - no need to sync via RPC
    }

    [ClientRpc]
    private void SyncCharacterDataClientRpc(
        string characterName,
        float maxHealth,
        float moveSpeed,
        float forwardMultiplier,
        string isMovingParam,
        string moveXParam,
        string moveYParam,
        Vector3 spawnPosition,
        Quaternion spawnRotation
    )
    {
        if (IsServer)
        {
            return; // Server already has this data
        }

        if (_world == null || _entity.Equals(default))
        {
            Debug.LogWarning("[NetworkSyncView] Cannot sync character data - entity not initialized");
            return;
        }

        Debug.Log(
            $"[NetworkSyncView] Syncing character data for {characterName} - Speed: {moveSpeed}, Health: {maxHealth}, Pos: {spawnPosition}"
        );

        // Update transform with correct spawn position
        if (_world.Components.TryGet(_entity, out TransformComponent trans))
        {
            trans.Position = spawnPosition;
            trans.Rotation = spawnRotation;

            // Also update Unity transform
            transform.position = spawnPosition;
            transform.rotation = spawnRotation;
        }

        // Update health
        if (_world.Components.TryGet(_entity, out HealthDataComponent health))
        {
            health.MaxHealth = maxHealth;
            health.CurrentHealth = maxHealth;
        }

        // Update movement with correct speed
        if (_world.Components.TryGet(_entity, out MovementDataComponent movement))
        {
            movement.MoveSpeed = moveSpeed;
            movement.ForwardMultiplier = forwardMultiplier;
            Debug.Log($"[NetworkSyncView] Updated movement speed to {moveSpeed}");
        }

        // Update animation params
        if (_world.Components.TryGet(_entity, out AnimationDataComponent anim))
        {
            anim.IsMovingParam = isMovingParam;
            anim.MoveXParam = moveXParam;
            anim.MoveYParam = moveYParam;
        }

        // Look up full CharacterData by name and populate skills
        CharacterDefinitionSO characterData = null;
        if (_spawnConfig == null)
        {
            Debug.LogError(
                $"[NetworkSyncView] _spawnConfig is NULL! Skills will not sync. Assign SpawnConfig to NetworkSyncView on player prefab."
            );
        }
        else if (_spawnConfig.possiblePlayers == null)
        {
            Debug.LogError($"[NetworkSyncView] _spawnConfig.possiblePlayers is NULL!");
        }
        else
        {
            Debug.Log(
                $"[NetworkSyncView] Looking for character '{characterName}' in {_spawnConfig.possiblePlayers.Length} possible players..."
            );
            foreach (var data in _spawnConfig.possiblePlayers)
            {
                if (data != null && data.characterName == characterName)
                {
                    characterData = data;
                    Debug.Log($"[NetworkSyncView] Found matching CharacterData for '{characterName}'");
                    break;
                }
            }
        }

        if (characterData != null)
        {
            // Update CharacterSelectionComponent with full CharacterData
            if (_world.Components.TryGet(_entity, out CharacterSelectionComponent charSel))
            {
                charSel.CharacterData = characterData;
                Debug.Log($"[NetworkSyncView] Client set CharacterData for {characterName}");
            }

            // Populate SkillSetComponent with skills from CharacterData
            if (_world.Components.TryGet(_entity, out SkillSetComponent skillSet))
            {
                skillSet.Skills.Clear();
                foreach (var skill in characterData.skills)
                {
                    skillSet.Skills.Add(skill);
                }
                // Also reset cooldowns
                skillSet.CooldownUntil = new float[characterData.skills.Count];
                Debug.Log($"[NetworkSyncView] Client synced {skillSet.Skills.Count} skills for {characterName}");
            }

            // Also populate AudioProfileComponent if available
            if (_world.Components.TryGet(_entity, out AudioProfileComponent audioProfile))
            {
                audioProfile.Profile = characterData.audioProfile;
            }
        }
        else
        {
            Debug.LogWarning(
                $"[NetworkSyncView] Could not find CharacterData for '{characterName}' in SpawnConfig. Skills will not work!"
            );
        }

        // NOW publish spawn event after all data is synced
        _world.Events.Publish(new PlayerSpawnEvent(_entity, gameObject, transform));

        Debug.Log($"[NetworkSyncView] Client fully synced character {characterName} at position {spawnPosition}");
    }

    #endregion
}
