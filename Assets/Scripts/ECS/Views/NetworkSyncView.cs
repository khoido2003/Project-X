using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class NetworkSyncView : NetworkBehaviour
{
    private World _world;
    private EntityId _entity;

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
    private Quaternion _previousRotation;
    private Quaternion _targerRotation;
    private float _lerpProgress;

    /////////////////////////////////////////////////////////////////////////////

    public void Initialize(World world, EntityId entity)
    {
        _world = world;
        _entity = entity;

        if (IsServer)
        {
            _world.Events.Subscribe<HealthChangedEvent>(OnHealthChanged);
            _world.Events.Subscribe<CombatStateChangedEvent>(OnCombatStateChanged);
            _world.Events.Subscribe<AnimationParameterEvent>(OnAnimationParameter);
            _world.Events.Subscribe<AttackExecutionRequestEvent>(OnAttackExecutionRequest);
        }

        if (IsClient)
        {
            _netTransform.OnValueChanged += OnNetTransformChanged;
            _netHealth.OnValueChanged += OnNetHealthChanged;
            _netCombatState.OnValueChanged += OnNetCombatStateChanged;
            _netMovement.OnValueChanged += OnNetMovementChanged;
        }
    }

    private void Start()
    {
        respawnUI = FindFirstObjectByType<PlayerRespawnUI>();
    }

    private void Update()
    {
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
        if (IsServer)
        {
            _world.Events.Unsubscribe<HealthChangedEvent>(OnHealthChanged);
            _world.Events.Unsubscribe<CombatStateChangedEvent>(OnCombatStateChanged);
            _world.Events.Unsubscribe<AnimationParameterEvent>(OnAnimationParameter);
            _world.Events.Unsubscribe<AttackExecutionRequestEvent>(OnAttackExecutionRequest);
        }

        if (IsClient)
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
        if (!_world.Components.TryGet(_entity, out NetworkOwnerComponent owner))
        {
            return;
        }

        if (_world.Components.TryGet(_entity, out MovementDataComponent movement))
        {
            var inputState = new ClientInputState
            {
                Tick = _currentTick,
                MoveInput = movement.InputDirection,
                MouseWorldPos = _world.Services.Resolve<IInputService>().GetMouseWorldPosition(),
            };

            _inputHistory.Enqueue(inputState);

            if (_inputHistory.Count > 60)
            {
                _inputHistory.Dequeue();
            }

            if (_currentTick % 2 == 0)
            {
                SendInputToServerRpc(inputState);
            }
        }
    }

    private void ClientInterpolation()
    {
        if (IsOwner || IsServer)
        {
            return;
        }

        _lerpProgress += Time.deltaTime * 10f;

        if (_world.Components.TryGet(_entity, out TransformComponent trans))
        {
            trans.Position = Vector3.Lerp(_previousPosition, _targetPosition, _lerpProgress);
            trans.Rotation = Quaternion.Lerp(_previousRotation, _targerRotation, _lerpProgress);

            // Sync to Unity Transform
            var registry = _world.Services.Resolve<EntityViewRegistry>();

            if (registry.TryGet(_entity, out EntityView view))
            {
                view.transform.position = trans.Position;
                view.transform.rotation = trans.Rotation;
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
        if (_world.Components.TryGet(_entity, out MovementDataComponent movement))
        {
            movement.InputDirection = input.MoveInput;
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
    public void RequestAttackServerRpc()
    {
        if (!_world.Components.TryGet(_entity, out AttackDataComponent attack))
        {
            RejectAttackClientRpc();
            return;
        }

        if (!_world.Components.TryGet(_entity, out WeaponDataComponent weapon))
        {
            RejectAttackClientRpc();
            return;
        }

        if (!attack.CanAttack(weapon.BaseCooldown) || attack.IsAttacking)
        {
            RejectAttackClientRpc();
            return;
        }

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

        Debug.LogWarning($"[Attack]: Server rejected attack for {_entity}");

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
        float damage
    )
    {
        if (IsServer)
        {
            return; // Server already processed
        }

        // Client-side: Play VFX/SFX only, no damage calculation
        // Damage is server-authoritative

        // ANIMATION

        if (!_world.Components.TryGet(_entity, out AttackDataComponent attack))
        {
            return;
        }

        if (!_world.Components.TryGet(_entity, out WeaponDataComponent weapon))
        {
            return;
        }

        int randomIndex = UnityEngine.Random.Range(0, weapon.TotalAttackAnimations);

        _world.Events.Publish(
            new AnimationParameterEvent(_entity, "attackIndex", AnimationParameterType.Float, randomIndex)
        );

        _world.Events.Publish(
            new AnimationParameterEvent(_entity, weapon.AttackAnimationTrigger, AnimationParameterType.Trigger, null)
        );
    }

    [ClientRpc]
    public void BroadcastDamageVisualClientRpc(float amount, Vector3 hitPoint)
    {
        if (IsServer)
        {
            return; // Server already knows
        }

        // Visual/audio feedback only
        // Spawn damage numbers, hit VFX, play damage sound
        Debug.Log($"Client received damage visual: {amount} at {hitPoint}");

        // TODO: Implement your damage visual feedback here
        // Example: Spawn floating damage text, hit particles, etc.
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

        // Simulate on server
        _world.Events.Publish(new EnterCombatStateEvent { Entity = _entity, TargetState = CombatState.CastingSkill });

        _world.Events.Publish(
            new SkillEffectTriggerEvent
            {
                Caster = _entity,
                Skill = buffer.Skill,
                TargetPoint = targetPoint,
                Direction = direction,
            }
        );

        BroadcastSkillExecutionClientRpc(targetPoint, direction);
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

        if (!_world.Components.TryGet(_entity, out TransformComponent transform))
        {
            return;
        }

        Vector3 knockback = direction.normalized * force;
        transform.Position += knockback * Time.deltaTime;

        var registry = _world.Services.Resolve<EntityViewRegistry>();
        if (registry.TryGet(_entity, out EntityView view))
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

        // Teleport camera
        var registry = _world.Services.Resolve<EntityViewRegistry>();
        if (registry.TryGet(_entity, out EntityView view))
        {
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

        // Trigger death visual/audio on clients
        _world.Events.Publish(new EntityDeathEvent(_entity));

        Debug.Log($"Client: Entity {_entity} died");

        // Play death effects
        var registry = _world.Services.Resolve<EntityViewRegistry>();
        if (registry.TryGet(_entity, out EntityView view))
        {
            // TODO: Play death animation, spawn death VFX
        }
    }

    [ClientRpc]
    public void ApplyDamageVisualClientRpc(float amount, Vector3 hitPoint)
    {
        // Visual/audio feedback only
        // Server already applied damage through DamageSystem

        // TODO: Spawn hit VFX, play damage sound, trigger hit reaction
    }

    [ClientRpc]
    private void SyncAnimationClientRpc(string paramsName, AnimationParameterType type, float value)
    {
        if (IsOwner)
        {
            return;
        }

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
        if (IsServer || IsOwner)
        {
            return;
        }
        _previousPosition = transform.position;
        _targetPosition = current.Position;

        _previousRotation = transform.rotation;
        _targerRotation = current.Rotation;

        _lerpProgress = 0f;
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

        BroadcastAttackExecutionClientRpc(@event.Type, @event.Direction, @event.Range, @event.Damage);
    }

    #endregion

    //////////////////////////////////////////////////

    #region Utils

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
