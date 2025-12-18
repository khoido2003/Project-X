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

    private uint _currentTick;
    private readonly Queue<ClientInputState> _inputHistory = new(60);

    private Vector3 _previousPosition;
    private Vector3 _targetPosition;
    private Quaternion _previousRotation = Quaternion.identity;
    private Quaternion _targerRotation = Quaternion.identity;
    private float _lerpProgress;

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
        world.Components.Add(clientEntity, new WeaponDataComponent());
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
        world.Components.Add(clientEntity, new PlayerUpgradesComponent());

        Debug.Log($"[NetworkSyncView] Client entity {clientEntity.Id} components added, requesting character data...");

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

            // update if changed significantly
            if (
                Vector3.Distance(_netTransform.Value.Position, newState.Position) > 0.01f
                || Quaternion.Angle(_netTransform.Value.Rotation, newState.Rotation) > 1f
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
                _netMovement.Value = new NetworkMovementState
                {
                    MoveDirection = movement.MoveDirection,
                    IsMoving = movement.IsMoving,
                    IsGrounded = movement.IsGrounded,
                    IsStunned = movement.IsStunned,
                };
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

        // Send to server EVERY frame for smooth movement
        // Previously only sent every other frame (_currentTick % 2 == 0)
        // This caused choppy movement on server
        SendInputToServerRpc(inputState);

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

        _lerpProgress += Time.deltaTime * 10f;

        // Update ECS TransformComponent, TransformSyncSystem will apply to Unity Transform
        // This prevents conflicts where both this method and TransformSyncSystem manipulate position
        if (_world.Components.TryGet(_entity, out TransformComponent trans))
        {
            // Interpolate position
            trans.Position = Vector3.Lerp(_previousPosition, _targetPosition, _lerpProgress);

            // Interpolate rotation with safety checks
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
                trans.Rotation = Quaternion.Lerp(_previousRotation, _targerRotation, _lerpProgress);
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

        // ANIMATION - ALL clients (including owner) get animation
        int randomIndex = UnityEngine.Random.Range(0, weapon.TotalAttackAnimations);

        _world.Events.Publish(
            new AnimationParameterEvent(_entity, "attackIndex", AnimationParameterType.Float, randomIndex)
        );

        _world.Events.Publish(
            new AnimationParameterEvent(_entity, weapon.AttackAnimationTrigger, AnimationParameterType.Trigger, null)
        );

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
    public void RequestSkillExecutionServerRpc(Vector3 targetPoint, Vector3 direction)
    {
        if (!_world.Components.TryGet(_entity, out SkillCastBufferComponent buffer))
        {
            return;
        }

        if (buffer.Skill == null)
        {
            return;
        }

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

        BroadcastSkillExecutionClientRpc(targetPoint, validatedDirection);
    }

    [ClientRpc]
    private void BroadcastSkillExecutionClientRpc(Vector3 targetPoint, Vector3 direction)
    {
        if (IsOwner)
        {
            return;
        }

        if (!_world.Components.TryGet(_entity, out SkillCastBufferComponent buffer))
        {
            return;
        }

        _world.Events.Publish(
            new SkillEffectTriggerEvent
            {
                Caster = _entity,
                Skill = buffer.Skill,
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
        if (!IsOwner)
        {
            return;
        }

        Debug.Log($"[Client] Player respawned at {spawnPosition}");

        // Hide respawn UI
        if (respawnUI == null)
        {
            Debug.LogError("respawnUI is null!");

            return;
        }

        respawnUI.HideRespawnTimer();

        // Show the character GameObject and teleport to spawn position
        var registry = _world.Services.Resolve<EntityViewRegistry>();
        if (registry.TryGet(_entity, out EntityView view))
        {
            view.gameObject.SetActive(true);
            view.transform.position = spawnPosition;
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
            // Remote clients: interpolate for smooth appearance
            _previousPosition = transform.position;
            _targetPosition = current.Position;

            _previousRotation = transform.rotation;
            _targerRotation = current.Rotation;

            _lerpProgress = 0f;
        }
    }

    private void OnAnimationParameter(AnimationParameterEvent @event)
    {
        if (!IsServer || @event.Entity != _entity)
        {
            return;
        }

        SyncAnimationClientRpc(@event.ParameterName, @event.ParameterType, SerializeValue(@event.Value));
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

        // NOW publish spawn event after all data is synced
        _world.Events.Publish(new PlayerSpawnEvent(_entity, gameObject, transform));

        Debug.Log($"[NetworkSyncView] Client fully synced character {characterName} at position {spawnPosition}");
    }

    #endregion
}
