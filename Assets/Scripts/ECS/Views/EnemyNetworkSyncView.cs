using System;
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

    private uint _currentTick;

    private Vector3 _previousPosition;
    private Vector3 _targetPosition;
    private Quaternion _previousRotation;
    private Quaternion _targetRotation;

    private float _lerpProgress;

    public void Initialize(World world, EntityId entity)
    {
        _world = world;
        _entity = entity;

        if (IsServer)
        {
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
    }

    private void Update()
    {
        if (IsServer)
        {
            ServerUpdate();
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

        ApplyTransformToUnity();
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

            if (
                Vector3.Distance(_netTransform.Value.Position, newState.Position) > 0.01f
                || Quaternion.Angle(_netTransform.Value.Rotation, newState.Rotation) > 1f
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

    private void ApplyTransformToUnity()
    {
        if (_world.Components.TryGet(_entity, out TransformComponent trans))
        {
            transform.SetPositionAndRotation(trans.Position, trans.Rotation);
        }
    }
    #endregion


    //////////////////////////////////////////////////////////////////////////////////

    #region Client


    private void ClientInterpolation()
    {
        if (IsServer)
        {
            return;
        }

        _lerpProgress += Time.deltaTime * 10f;

        if (_world.Components.TryGet(_entity, out TransformComponent trans))
        {
            {
                trans.Position = Vector3.Lerp(_previousPosition, _targetPosition, _lerpProgress);

                trans.Rotation = Quaternion.Lerp(_previousRotation, _targetRotation, _lerpProgress);

                var registry = _world.Services.Resolve<EntityViewRegistry>();
                if (registry.TryGet(_entity, out EntityView view))
                {
                    view.transform.position = trans.Position;
                    view.transform.rotation = trans.Rotation;
                }
            }
        }
    }

    #endregion

    //////////////////////////////////////////////////////////////////////////////

    #region RPC

    [ClientRpc]
    private void SyncAnimationClientRpc(string paramName, AnimationParameterType type, float value)
    {
        if (IsServer)
        {
            return;
        }

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

        Debug.Log($"[EnemyNetworkSync]: Client received attack for {_entity}");

        // Play VFX/Animation

        // Client-side: Execute visual-only attack
        if (!_world.Components.TryGet(_entity, out WeaponDataComponent weapon))
        {
            return;
        }

        // Publish attack execution event for visual effects ONLY
        // No damage calculation on client
        _world.Events.Publish(
            new AttackExecutionRequestEvent
            {
                Attacker = _entity,
                Type = type,
                Direction = direction,
                Range = range,
                Damage = 0f, // No damage on client
                ImpactEffect = weapon.HitImpactParticlePrefab,
                ProjectilePrefab = weapon.ProjectilePrefab,
                ProjectileSpeed = projectileSpeed,
                ProjectileLifetime = projectileLifetime,
                SpawnOffset = spawnOffset,
            }
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
        if (IsServer || !IsSpawned)
        {
            return;
        }

        _world.Events.Publish(new EntityDeathEvent(_entity));

        Debug.Log($"[EnemyNetworkSync] Client: Enemy {_entity} died");
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

            // Don't run full state logic on clients, just visual updates
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
        if (IsServer)
        {
            return;
        }

        // Initialize on first sync
        if (_previousRotation == Quaternion.identity && _targetRotation == Quaternion.identity)
        {
            _previousRotation = current.Rotation;
            _targetRotation = current.Rotation;
            _previousPosition = current.Position;
            _targetPosition = current.Position;

            transform.SetPositionAndRotation(current.Position, current.Rotation);
            return;
        }
        _previousPosition = transform.position;
        _targetPosition = current.Position;

        _previousRotation = transform.rotation;
        _targetRotation = current.Rotation;

        _lerpProgress = 0f;
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
