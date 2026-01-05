using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class EnemyNetworkSyncView : NetworkBehaviour
{
    private World _world;
    private EntityId _entity;

    private NetworkVariable<NetworkTransformState> _netTransform = new(
        writePerm: NetworkVariableWritePermission.Server
    );

    private NetworkVariable<NetworkHealthState> _netHealth = new(writePerm: NetworkVariableWritePermission.Server);

    private NetworkVariable<EnemyState> _netState = new(writePerm: NetworkVariableWritePermission.Server);

    private NetworkVariable<bool> _netHasTarget = new(writePerm: NetworkVariableWritePermission.Server);

    private NetworkVariable<NetworkMovementState> _netMovement = new(writePerm: NetworkVariableWritePermission.Server);

    // Serialized weapon data - populated from EnemyDefinitionSO by EnemyFactory on server
    // These are available on clients since they're part of the prefab
    [Header("Weapon Configuration (Set by EnemyFactory)")]
    [SerializeField]
    private AttackExecutionType _executionType;

    [SerializeField]
    private GameObject _projectilePrefab;

    [SerializeField]
    private ParticleSystem _hitImpactParticlePrefab;

    [SerializeField]
    private float _projectileSpeed = 10f;

    [SerializeField]
    private float _projectileLifetime = 3f;

    [SerializeField]
    private Vector3 _projectileSpawnOffset;

    [SerializeField]
    private string _attackAnimationTrigger = "attack";

    [Header("Boss VFX (Configure on Boss Prefab)")]
    [SerializeField]
    private ParticleSystem _bossJumpLandingVFX;

    [SerializeField]
    private ParticleSystem _bossFlamethrowerVFX;

    [Header("Boss Audio (Configure on Boss Prefab)")]
    [SerializeField]
    private AudioClip _bossJumpSound;

    [SerializeField]
    private AudioClip _bossLandingSound;

    [SerializeField]
    private AudioClip _bossFlamethrowerSound;

    [SerializeField]
    private AudioClip _bossAttackSound;

    [Header("Enemy Audio Profile (Configure on Enemy Prefab)")]
    [SerializeField]
    [Tooltip("Audio profile for enemy sounds - set this to enable client-side audio")]
    private AudioProfileSO _enemyAudioProfile;

    // Active flamethrower VFX instance (for start/stop)
    private ParticleSystem _activeFlameVFX;

    private uint _currentTick;

    private Vector3 _previousPosition;
    private Vector3 _targetPosition;
    private Quaternion _previousRotation;
    private Quaternion _targetRotation;

    private float _interpolationTime;
    private float _interpolationDuration = 0.1f; // Time between expected network updates

    // OPTIMIZATION: Cache last synced animation values to throttle RPCs
    // Only broadcasts when values actually change
    private Dictionary<string, float> _lastSyncedAnimValues = new();

    // OPTIMIZATION: Cache last synced state values to throttle NetworkVariable updates
    private EnemyState _lastSyncedState;
    private bool _lastSyncedHasTarget;
    private NetworkMovementState _lastSyncedMovement;

    private bool _isInitialized = false;
    private bool _firstTransformReceived = false;

    // Network optimization: velocity-based dead reckoning
    private Vector3 _estimatedVelocity;
    private Vector3 _smoothedVelocity; // Dampened velocity for smoother extrapolation
    private float _lastNetworkUpdateTime;
    private uint _lastReceivedTick;

    // Constants for interpolation tuning
    private const float SNAP_DISTANCE_THRESHOLD = 5f; // Snap if too far (teleport)
    private const float VELOCITY_SMOOTHING = 0.3f; // How much to smooth velocity changes
    private const float MAX_EXTRAPOLATION_TIME = 0.15f; // Max time to extrapolate beyond target

    // Public setters for EnemyFactory to configure weapon data
    public void SetWeaponData(
        AttackExecutionType executionType,
        GameObject projectilePrefab,
        ParticleSystem hitImpactPrefab,
        float projectileSpeed,
        float projectileLifetime,
        Vector3 spawnOffset,
        string attackTrigger
    )
    {
        _executionType = executionType;
        _projectilePrefab = projectilePrefab;
        _hitImpactParticlePrefab = hitImpactPrefab;
        _projectileSpeed = projectileSpeed;
        _projectileLifetime = projectileLifetime;
        _projectileSpawnOffset = spawnOffset;
        _attackAnimationTrigger = attackTrigger;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // On SERVER: Initialize is called by EnemyFactory after CreateEntity
        // On CLIENT: We need to create the ECS entity here since EnemyFactory doesn't run
        if (!IsServer && IsClient)
        {
            StartCoroutine(WaitForWorldAndCreateEntity());
        }
        else
        {
            // Server initialization handled by EnemyFactory
        }
    }

    private System.Collections.IEnumerator WaitForWorldAndCreateEntity()
    {
        // Wait for World to be ready
        float timeout = 5f;
        float elapsed = 0f;
        while (WorldRunner.Instance?.World == null && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (WorldRunner.Instance?.World == null)
        {
            Debug.LogError("[EnemyNetworkSyncView] Client: World STILL null after timeout!");
            yield break;
        }

        CreateClientEntity();
    }

    private void CreateClientEntity()
    {
        _world = WorldRunner.Instance?.World;
        if (_world == null)
        {
            Debug.LogError("[EnemyNetworkSyncView] Client: World is null, cannot create entity");
            return;
        }

        _entity = _world.CreateEntity();

        // Bind all EntityViews
        foreach (EntityView view in GetComponentsInChildren<EntityView>(includeInactive: true))
        {
            view.Bind(_world, _entity);
            var registry = _world.Services.Resolve<EntityViewRegistry>();
            registry?.Register(view);
        }

        // Add essential client-side components
        Vector3 pos = _netTransform.Value.Position;
        Quaternion rot = _netTransform.Value.Rotation;
        if (pos == Vector3.zero && rot == default)
        {
            pos = transform.position;
            rot = transform.rotation;
        }

        _previousPosition = pos;
        _targetPosition = pos;
        _previousRotation = rot == default ? Quaternion.identity : rot;
        _targetRotation = _previousRotation;

        _world.Components.Add(_entity, new TransformComponent(pos, _previousRotation));
        _world.Components.Add(
            _entity,
            new HealthDataComponent { CurrentHealth = _netHealth.Value.Current, MaxHealth = _netHealth.Value.Max }
        );
        _world.Components.Add(
            _entity,
            new MovementDataComponent { MoveSpeed = 3f, IsMoving = _netMovement.Value.IsMoving }
        );
        _world.Components.Add(
            _entity,
            new EnemyComponent
            {
                CurrentState = _netState.Value,
                PatrolWaypoints = new List<Vector3>(),
                Path = new List<Vector3>(),
            }
        );
        _world.Components.Add(
            _entity,
            new AnimationDataComponent { IsMovingParam = "isMoving", AttackTrigger = _attackAnimationTrigger }
        );

        // Add WeaponDataComponent for client-side projectile spawning
        // Uses serialized fields configured on the enemy prefab in Unity inspector
        _world.Components.Add(_entity, new AttackDataComponent { IsPlayerControlled = false });
        _world.Components.Add(
            _entity,
            new WeaponDataComponent
            {
                WeaponName = "EnemyAttack",
                ExecutionType = _executionType,
                AttackAnimationTrigger = _attackAnimationTrigger,
                TotalAttackAnimations = 1,
                ProjectilePrefab = _projectilePrefab,
                HitImpactParticlePrefab = _hitImpactParticlePrefab,
                ProjectileSpeed = _projectileSpeed,
                ProjectileLifetime = _projectileLifetime,
                ProjectileSpawnOffset = _projectileSpawnOffset,
            }
        );

        // Add AudioProfileComponent with the serialized profile from prefab
        // This allows clients to play enemy sounds locally
        _world.Components.Add(_entity, new AudioProfileComponent { Profile = _enemyAudioProfile });

        // Subscribe to NetworkVariable changes
        _netTransform.OnValueChanged += OnNetTransformChanged;
        _netHealth.OnValueChanged += OnNetHealthChanged;
        _netState.OnValueChanged += OnNetStateChanged;
        _netHasTarget.OnValueChanged += OnNetHasTargetChanged;
        _netMovement.OnValueChanged += OnNetMovementChanged;

        _isInitialized = true;
        _firstTransformReceived = true; // Already have initial position

        // CRITICAL: Apply current NetworkVariable values immediately!
        // OnValueChanged doesn't fire for values already set before subscription
        ApplyInitialNetworkValues();

        // Removed Debug.Log - was spamming during mass enemy spawns
    }

    public void Initialize(World world, EntityId entity)
    {
        _world = world;
        _entity = entity;

        if (_world.Components.TryGet(_entity, out TransformComponent trans))
        {
            _previousPosition = trans.Position;
            _targetPosition = trans.Position;
            _previousRotation = trans.Rotation;
            _targetRotation = trans.Rotation;
        }
        if (IsServer)
        {
            if (IsServer)
            {
                if (_world.Components.TryGet(_entity, out trans))
                {
                    _netTransform.Value = new NetworkTransformState
                    {
                        Position = trans.Position,
                        Rotation = trans.Rotation,
                        Tick = _currentTick,
                    };
                }
            }

            _world.Events.Subscribe<HealthChangedEvent>(OnHealthChanged);
            _world.Events.Subscribe<AnimationParameterEvent>(OnAnimationParameter);
            _world.Events.Subscribe<AttackExecutionRequestEvent>(OnAttackExecutionRequest);
        }

        if (IsClient)
        {
            _netTransform.OnValueChanged += OnNetTransformChanged;
            _netHealth.OnValueChanged += OnNetHealthChanged;
            _netState.OnValueChanged += OnNetStateChanged;
            _netHasTarget.OnValueChanged += OnNetHasTargetChanged;
            _netMovement.OnValueChanged += OnNetMovementChanged;
        }

        _isInitialized = true;
    }

    private void Update()
    {
        if (!_isInitialized)
        {
            return;
        }

        if (IsServer)
        {
            ServerUpdate();
        }
        else
        {
            // CLIENT: Interpolate enemy position for smooth movement
            ClientInterpolation();
        }
    }

    private void ClientInterpolation()
    {
        if (!_firstTransformReceived)
            return;

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

        // Smooth rotation interpolation
        float rotT = Mathf.Clamp01(t * 1.2f); // Rotation completes slightly faster
        trans.Rotation = Quaternion.Slerp(_previousRotation, _targetRotation, rotT);
    }

    private void FixedUpdate()
    {
        _currentTick++;
    }

    public override void OnNetworkDespawn()
    {
        // CRITICAL: Destroy the client-side ECS entity to prevent memory leak!
        // Without this, client entities accumulate and cause FPS degradation
        if (!IsServer && _world != null && !_entity.Equals(default))
        {
            try
            {
                // Unregister views first
                var registry = _world.Services.Resolve<EntityViewRegistry>();
                foreach (EntityView view in GetComponentsInChildren<EntityView>(includeInactive: true))
                {
                    registry?.Unregister(view);
                }

                _world.DestroyEntity(_entity);
            }
            catch (System.Exception ex)
            {
                // Silently ignore - entity might already be destroyed
            }
        }

        if (IsServer)
        {
            _world.Events.Unsubscribe<HealthChangedEvent>(OnHealthChanged);
            _world.Events.Unsubscribe<AnimationParameterEvent>(OnAnimationParameter);
            _world.Events.Unsubscribe<AttackExecutionRequestEvent>(OnAttackExecutionRequest);
        }

        if (IsClient)
        {
            _netTransform.OnValueChanged -= OnNetTransformChanged;
            _netHealth.OnValueChanged -= OnNetHealthChanged;
            _netState.OnValueChanged -= OnNetStateChanged;
            _netHasTarget.OnValueChanged -= OnNetHasTargetChanged;
            _netMovement.OnValueChanged -= OnNetMovementChanged;
        }
    }

    /////////////////////////////////////////////////////////////////////////////

    #region Server

    private void ServerUpdate()
    {
        // OPTIMIZATION: Distance-based sync rate
        // Far enemies sync less frequently to reduce bandwidth
        int syncInterval = GetDistanceBasedSyncInterval();

        if (_currentTick % syncInterval == 0)
        {
            SyncTransform();
        }

        SyncEnemyState();
        SyncMovement();
    }

    /// <summary>
    /// Returns sync interval based on distance to nearest player.
    /// Close: every tick, Medium: every 2 ticks, Far: every 4 ticks
    /// </summary>
    private int GetDistanceBasedSyncInterval()
    {
        float nearestPlayerDist = float.MaxValue;

        // Find nearest player
        foreach (
            var (playerEntity, player, playerTf) in _world.Components.Query<PlayerTagComponent, TransformComponent>()
        )
        {
            float dist = Vector3.Distance(transform.position, playerTf.Position);
            if (dist < nearestPlayerDist)
            {
                nearestPlayerDist = dist;
            }
        }

        // Distance thresholds
        if (nearestPlayerDist > 40f)
            return 4; // Very far: sync every 4 ticks (~15Hz)
        if (nearestPlayerDist > 20f)
            return 2; // Medium: sync every 2 ticks (~30Hz)
        return 1; // Close: sync every tick (~60Hz)
    }

    private void SyncTransform()
    {
        if (_world.Components.TryGet(_entity, out TransformComponent trans))
        {
            transform.SetPositionAndRotation(trans.Position, trans.Rotation);

            var newState = new NetworkTransformState
            {
                Position = trans.Position,
                Rotation = trans.Rotation,
                Tick = _currentTick,
            };

            // OPTIMIZATION: Only sync if position/rotation changed significantly
            if (
                Vector3.Distance(_netTransform.Value.Position, newState.Position) > 0.05f
                || Quaternion.Angle(_netTransform.Value.Rotation, newState.Rotation) > 3f
            )
            {
                _netTransform.Value = newState;
            }
        }
    }

    private void SyncEnemyState()
    {
        if (_currentTick % 2 == 0)
        {
            if (_world.Components.TryGet(_entity, out EnemyComponent enemy))
            {
                // OPTIMIZATION: Only sync if values actually changed
                if (enemy.CurrentState != _lastSyncedState)
                {
                    _netState.Value = enemy.CurrentState;
                    _lastSyncedState = enemy.CurrentState;
                }

                bool hasTarget = !enemy.TargetEntity.Equals(default);
                if (hasTarget != _lastSyncedHasTarget)
                {
                    _netHasTarget.Value = hasTarget;
                    _lastSyncedHasTarget = hasTarget;
                }
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
                    newState.IsMoving != _lastSyncedMovement.IsMoving
                    || newState.IsGrounded != _lastSyncedMovement.IsGrounded
                    || newState.IsStunned != _lastSyncedMovement.IsStunned
                    || (
                        newState.IsMoving
                        && Vector3.SqrMagnitude(newState.MoveDirection - _lastSyncedMovement.MoveDirection) > 0.01f
                    );

                if (changed)
                {
                    _netMovement.Value = newState;
                    _lastSyncedMovement = newState;
                }
            }
        }
    }

    #endregion


    //////////////////////////////////////////////////////////////////////////////////

    #region RPC

    [ClientRpc]
    private void SyncAnimationClientRpc(string paramName, AnimationParameterType type, float value)
    {
        if (IsServer)
        {
            return;
        }

        // Removed Debug.Log - was spamming ~2k+ times per second causing FPS drop
        object deserializeValue = DeserializeValue(type, value);

        _world.Events.Publish(new AnimationParameterEvent(_entity, paramName, type, deserializeValue));
    }

    [ClientRpc]
    private void BroadcastAttackExecutionClientRpc(
        AttackExecutionType type,
        Vector3 direction,
        Vector3 origin,
        float range,
        float damage,
        float projectileSpeed,
        float projectileLifetime,
        Vector3 spawnOffset
    )
    {
        if (IsServer)
        {
            return;
        }

        // Client-side: Execute visual-only attack
        if (!_world.Components.TryGet(_entity, out WeaponDataComponent weapon))
        {
            Debug.LogWarning(
                $"[EnemyNetworkSyncView] BroadcastAttackExecutionClientRpc: No WeaponDataComponent for entity {_entity.Id}"
            );
            return;
        }

        // ANIMATION - Trigger attack animation on client
        _world.Events.Publish(
            new AnimationParameterEvent(_entity, weapon.AttackAnimationTrigger, AnimationParameterType.Trigger, null)
        );

        // PROJECTILE - Spawn visual-only projectile on client
        if (type == AttackExecutionType.Projectile && weapon.ProjectilePrefab != null)
        {
            SpawnClientProjectile(direction, origin, projectileSpeed, projectileLifetime, spawnOffset, weapon);
        }
    }

    /// <summary>
    /// Spawns a visual-only projectile on the client for enemy attacks
    /// </summary>
    private void SpawnClientProjectile(
        Vector3 direction,
        Vector3 origin,
        float speed,
        float lifetime,
        Vector3 spawnOffset,
        WeaponDataComponent weapon
    )
    {
        // Calculate spawn position
        Vector3 forwardDir = direction.sqrMagnitude < 0.0001f ? transform.forward : direction.normalized;
        forwardDir.y = 0f;
        forwardDir = forwardDir.normalized;

        // Apply spawn offset
        Vector3 spawnPos = origin + new Vector3(0f, 1.0f, 0f); // Default height
        if (spawnOffset.sqrMagnitude > 0.0001f)
        {
            spawnPos += Quaternion.LookRotation(forwardDir, Vector3.up) * spawnOffset;
        }

        Quaternion spawnRot = Quaternion.LookRotation(forwardDir, Vector3.up);

        // Get projectile from pool
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
            0f, // NO DAMAGE on client
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
    public void BroadcastDamageVisualClientRpc(float amount, Vector3 hitpoint)
    {
        if (IsServer || !IsSpawned)
        {
            return;
        }

        // Removed Debug.Log - was spamming during combat
    }

    [ClientRpc]
    public void BroadcastDeathClientRpc()
    {
        // Add safety check
        if (!IsSpawned || NetworkObject == null)
        {
            Debug.LogWarning("BroadcastDeathClientRpc called on invalid NetworkObject");
            return;
        }

        if (IsServer)
        {
            return;
        }

        _world.Events.Publish(new EntityDeathEvent(_entity));

        Debug.Log($"[EnemyNetworkSync] Client: Enemy {_entity} died");
    }

    /// <summary>
    /// Broadcasts boss jump landing VFX to all clients
    /// </summary>
    [ClientRpc]
    public void BroadcastBossJumpLandingVfxClientRpc(Vector3 position)
    {
        if (IsServer)
            return;

        // Use serialized prefab from this view (available on prefab, works on clients!)
        if (_bossJumpLandingVFX != null)
        {
            var vfx = Instantiate(_bossJumpLandingVFX, position, Quaternion.identity);
            vfx.Play();
            Destroy(vfx.gameObject, 3f);
        }
    }

    /// <summary>
    /// Broadcasts boss flamethrower VFX start/stop to all clients
    /// </summary>
    [ClientRpc]
    public void BroadcastBossFlamethrowerVfxClientRpc(bool isStarting)
    {
        if (IsServer)
            return;

        if (isStarting && _bossFlamethrowerVFX != null)
        {
            // Spawn and attach to boss
            _activeFlameVFX = Instantiate(_bossFlamethrowerVFX, transform);
            _activeFlameVFX.transform.localPosition = new Vector3(0, 1.5f, 0.5f);
            _activeFlameVFX.Play();
        }
        else if (!isStarting && _activeFlameVFX != null)
        {
            // Stop and destroy
            _activeFlameVFX.Stop();
            Destroy(_activeFlameVFX.gameObject, 1f);
            _activeFlameVFX = null;
        }
    }

    /// <summary>
    /// Broadcasts boss audio to all clients.
    /// audioType: 0 = Jump, 1 = Landing, 2 = Flamethrower, 3 = Attack
    /// </summary>
    [ClientRpc]
    public void BroadcastBossAudioClientRpc(int audioType, Vector3 position)
    {
        if (IsServer)
            return;

        AudioClip clip = audioType switch
        {
            0 => _bossJumpSound,
            1 => _bossLandingSound,
            2 => _bossFlamethrowerSound,
            3 => _bossAttackSound,
            _ => null
        };

        if (clip != null)
        {
            // Use AudioHelper to route through audio service (mixer, volume control, pooling)
            // instead of raw AudioSource.PlayClipAtPoint which bypasses mixer
            AudioHelper.PlaySound3D(clip, AudioCategory.Enemy, position, 1.0f);
        }
    }

    #endregion


    ////////////////////////////////////////////////////////////////////////////////

    #region Client Value Initialization

    /// <summary>
    /// Apply current NetworkVariable values to ECS components.
    /// Called immediately after subscribing since OnValueChanged doesn't fire for already-set values.
    /// </summary>
    private void ApplyInitialNetworkValues()
    {
        // Removed Debug.Log - spamming during enemy spawns

        // Apply transform
        var transformState = _netTransform.Value;
        if (_world.Components.TryGet(_entity, out TransformComponent trans))
        {
            trans.Position = transformState.Position;
            trans.Rotation = transformState.Rotation;
            // Removed Debug.Log - spamming during enemy spawns
        }
        transform.SetPositionAndRotation(transformState.Position, transformState.Rotation);
        _targetPosition = transformState.Position;
        _targetRotation = transformState.Rotation;
        _previousPosition = transformState.Position;
        _previousRotation = transformState.Rotation;

        // Apply health
        var healthState = _netHealth.Value;
        if (_world.Components.TryGet(_entity, out HealthDataComponent health))
        {
            health.CurrentHealth = healthState.Current;
            health.MaxHealth = healthState.Max;
        }

        // Apply enemy state
        var state = _netState.Value;
        if (_world.Components.TryGet(_entity, out EnemyComponent enemy))
        {
            enemy.CurrentState = state;
        }

        // Apply movement
        var movementState = _netMovement.Value;
        if (_world.Components.TryGet(_entity, out MovementDataComponent movement))
        {
            movement.MoveDirection = movementState.MoveDirection;
            movement.IsMoving = movementState.IsMoving;
            movement.IsGrounded = movementState.IsGrounded;
            movement.IsStunned = movementState.IsStunned;
        }
    }

    #endregion


    ////////////////////////////////////////////////////////////////////////////////

    #region Callbacks

    private void OnNetHasTargetChanged(bool prev, bool current)
    {
        if (IsServer)
        {
            return;
        }

        // Removed Debug.Log - was spamming on every target change
    }

    private void OnNetStateChanged(EnemyState prev, EnemyState current)
    {
        if (IsServer)
        {
            return;
        }

        if (_world.Components.TryGet(_entity, out EnemyComponent enemy))
        {
            enemy.CurrentState = current;

            // Spawn ragdoll on client when enemy dies
            if (current == EnemyState.Dead && !enemy.RagdollSpawned)
            {
                var ragdollRef = GetComponentInChildren<RagdollReference>();
                if (ragdollRef != null)
                {
                    RagdollUtility.ActivateRagdoll(ragdollRef.gameObject);
                    // Only log important state changes like death
                }
                enemy.RagdollSpawned = true;
            }

            // Removed frequent Debug.Log - was spamming on every state change
        }
    }

    private void OnNetMovementChanged(NetworkMovementState prev, NetworkMovementState current)
    {
        if (IsServer)
        {
            return;
        }

        if (_world.Components.TryGet(_entity, out MovementDataComponent movement))
        {
            movement.MoveDirection = current.MoveDirection;
            movement.IsMoving = current.IsMoving;
            movement.IsGrounded = current.IsGrounded;
            movement.IsStunned = current.IsStunned;

            // Update animations based on movement
            if (_world.Components.TryGet(_entity, out AnimationDataComponent anim))
            {
                _world.Events.Publish(
                    new AnimationParameterEvent(
                        _entity,
                        anim.IsMovingParam,
                        AnimationParameterType.Bool,
                        current.IsMoving
                    )
                );
            }
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
        if (IsServer || !_isInitialized)
        {
            return;
        }

        if (!_firstTransformReceived)
        {
            _firstTransformReceived = true;

            _previousPosition = current.Position;
            _targetPosition = current.Position;
            _previousRotation = current.Rotation;
            _targetRotation = current.Rotation;
            _lastReceivedTick = current.Tick;
            _lastNetworkUpdateTime = Time.time;

            if (_world.Components.TryGet(_entity, out TransformComponent trans))
            {
                trans.Position = current.Position;
                trans.Rotation = current.Rotation;
            }

            transform.SetPositionAndRotation(current.Position, current.Rotation);
            return;
        }

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
            _targetRotation = current.Rotation;
            _smoothedVelocity = Vector3.zero;
            _interpolationTime = _interpolationDuration; // Already at target

            if (_world.Components.TryGet(_entity, out TransformComponent trans))
            {
                trans.Position = current.Position;
                trans.Rotation = current.Rotation;
            }
            return;
        }

        // SMOOTH TRANSITION: Start from current VISUAL position, not old target
        // This prevents snapping back when network updates arrive
        if (_world.Components.TryGet(_entity, out TransformComponent tf))
        {
            _previousPosition = tf.Position;
            _previousRotation = tf.Rotation;
        }
        else
        {
            _previousPosition = transform.position;
            _previousRotation = transform.rotation;
        }

        _targetPosition = current.Position;
        _targetRotation = current.Rotation;

        // Reset interpolation time to start new interpolation from current position
        _interpolationTime = 0f;
    }

    private void OnAttackExecutionRequest(AttackExecutionRequestEvent @event)
    {
        if (!IsServer || @event.Attacker != _entity)
        {
            return;
        }

        Vector3 origin = transform.position;
        if (_world.Components.TryGet(_entity, out TransformComponent trans))
        {
            origin = trans.Position;
        }

        // Broadcast attack to all clients with full details
        BroadcastAttackExecutionClientRpc(
            @event.Type,
            @event.Direction,
            origin,
            @event.Range,
            @event.Damage,
            @event.ProjectileSpeed,
            @event.ProjectileLifetime,
            @event.SpawnOffset
        );
    }

    private void OnAnimationParameter(AnimationParameterEvent @event)
    {
        if (!IsServer || @event.Entity != _entity)
        {
            return;
        }

        // OPTIMIZATION: Only sync if value actually changed
        // This prevents ~300+ RPCs per frame with 100 enemies
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

    private void OnHealthChanged(HealthChangedEvent @event)
    {
        if (!IsServer || @event.Entity != _entity)
        {
            return;
        }

        _netHealth.Value = new NetworkHealthState { Current = @event.CurrentHealth, Max = @event.MaxHealth };
    }

    #endregion


    #region UTILS

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
}
