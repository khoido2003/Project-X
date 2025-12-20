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

    private uint _currentTick;

    private Vector3 _previousPosition;
    private Vector3 _targetPosition;
    private Quaternion _previousRotation;
    private Quaternion _targetRotation;

    private float _lerpProgress;
    private bool _isInitialized = false;
    private bool _firstTransformReceived = false;

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
            Debug.Log("[EnemyNetworkSyncView] Client-side spawn detected, calling CreateClientEntity()");
            StartCoroutine(WaitForWorldAndCreateEntity());
        }
        else
        {
            Debug.Log($"[EnemyNetworkSyncView] Skipping CreateClientEntity - IsServer: {IsServer}");
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

        Debug.Log($"[EnemyNetworkSyncView] Client created ECS entity {_entity.Id} for enemy at {pos}");
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
    }

    private void FixedUpdate()
    {
        _currentTick++;
    }

    public override void OnNetworkDespawn()
    {
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
        SyncTransform();
        SyncEnemyState();
        SyncMovement();
    }

    private void SyncTransform()
    {
        if (_world.Components.TryGet(_entity, out TransformComponent trans))
        {
            transform.SetPositionAndRotation(trans.Position, trans.Rotation);

            _netTransform.Value = new NetworkTransformState
            {
                Position = trans.Position,
                Rotation = trans.Rotation,
                Tick = _currentTick,
            };
        }
    }

    private void SyncEnemyState()
    {
        if (_currentTick % 2 == 0)
        {
            if (_world.Components.TryGet(_entity, out EnemyComponent enemy))
            {
                _netState.Value = enemy.CurrentState;
                _netHasTarget.Value = !enemy.TargetEntity.Equals(default);
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


    //////////////////////////////////////////////////////////////////////////////////

    #region RPC

    [ClientRpc]
    private void SyncAnimationClientRpc(string paramName, AnimationParameterType type, float value)
    {
        if (IsServer)
        {
            return;
        }

        Debug.Log(
            $"[EnemyNetworkSyncView] SyncAnimationClientRpc: entity {_entity.Id}, param: {paramName}, type: {type}"
        );

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

        Debug.Log($"[EnemyNetworkSync] Client Received damage visual: {amount}  at {hitpoint}");
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

    #endregion


    ////////////////////////////////////////////////////////////////////////////////

    #region Client Value Initialization

    /// <summary>
    /// Apply current NetworkVariable values to ECS components.
    /// Called immediately after subscribing since OnValueChanged doesn't fire for already-set values.
    /// </summary>
    private void ApplyInitialNetworkValues()
    {
        Debug.Log($"[EnemyNetworkSyncView] ApplyInitialNetworkValues for entity {_entity.Id}");

        // Apply transform
        var transformState = _netTransform.Value;
        if (_world.Components.TryGet(_entity, out TransformComponent trans))
        {
            trans.Position = transformState.Position;
            trans.Rotation = transformState.Rotation;
            Debug.Log($"[EnemyNetworkSyncView] Applied initial transform: pos={transformState.Position}");
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

        // Client can react to enemy having/losing target
        Debug.Log($"[EnemyNetworkSync] Enemy {_entity} has target: {current}");
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
                    Debug.Log($"[EnemyNetworkSync] Client spawned ragdoll for enemy {_entity.Id}");
                }
                enemy.RagdollSpawned = true;
            }

            Debug.Log($"[EnemyNetworkSync] Enemy {_entity} state changed: {prev} -> {current}");
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

            if (_world.Components.TryGet(_entity, out TransformComponent trans))
            {
                trans.Position = current.Position;
                trans.Rotation = current.Rotation;
            }

            transform.SetPositionAndRotation(current.Position, current.Rotation);
            return;
        }

        _previousPosition = _targetPosition;
        _targetPosition = current.Position;
        _previousRotation = _targetRotation;
        _targetRotation = current.Rotation;

        _lerpProgress = 0f;

        // Update ECS TransformComponent on EVERY position change
        // EnemyMovementView reads from ECS to apply to Unity Transform with interpolation
        if (_world.Components.TryGet(_entity, out TransformComponent transComponent))
        {
            transComponent.Position = current.Position;
            transComponent.Rotation = current.Rotation;
        }
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

        SyncAnimationClientRpc(@event.ParameterName, @event.ParameterType, SerializeValue(@event.Value));
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
