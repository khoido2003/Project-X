using Unity.Netcode;
using UnityEngine;

public class AttackSystem : ISystem
{
    private World _world;

    public void Initialize(World world)
    {
        _world = world;
        _world.Events.Subscribe<AttackPressedInputEvent>(OnAttackRequest);
        _world.Events.Subscribe<AnimationEventRelayEvent>(OnAnimationRelayEvent);
    }

    public void Update(float dt) { }

    private void OnAttackRequest(AttackPressedInputEvent @event)
    {
        // Check basic components first
        if (!_world.Components.TryGet(@event.Entity, out AttackDataComponent attack))
        {
            return;
        }

        if (!_world.Components.TryGet(@event.Entity, out WeaponDataComponent weapon))
        {
            return;
        }

        if (!_world.Components.TryGet(@event.Entity, out CombatStateComponent state))
        {
            state = new CombatStateComponent { CurrentState = CombatState.Idle };
            _world.Components.Add(@event.Entity, state);
        }

        // Block attack if already attacking - prevents animation spam/reset
        if (state.CurrentState == CombatState.Attacking || attack.IsAttacking)
        {
            return;
        }

        // Check cooldown
        if (!attack.CanAttack(weapon.BaseCooldown))
        {
            return;
        }

        // CLIENT-SIDE PREDICTION: Play animation immediately for responsive feel
        // This runs on BOTH client and server for immediate feedback
        bool isLocalPlayer = _world.Components.TryGet(@event.Entity, out NetworkOwnerComponent owner) && owner.IsLocalPlayer;
        
        if (isLocalPlayer || NetworkManager.Singleton.IsServer)
        {
            // Update local combat state for immediate visual feedback
            state.CurrentState = CombatState.Attacking;
            attack.IsAttacking = true;
            
            // Play attack animation immediately
            int randomIndex = UnityEngine.Random.Range(0, weapon.TotalAttackAnimations);
            _world.Events.Publish(
                new AnimationParameterEvent(@event.Entity, "attackIndex", AnimationParameterType.Float, randomIndex)
            );
            _world.Events.Publish(
                new AnimationParameterEvent(
                    @event.Entity,
                    weapon.AttackAnimationTrigger,
                    AnimationParameterType.Trigger,
                    null
                )
            );
        }

        // SERVER-ONLY: Process actual attack logic (damage, projectiles, etc.)
        if (!NetworkManager.Singleton.IsServer)
        {
            return; // Client stops here after playing animation
        }

        if (
            _world.Components.TryGet(@event.Entity, out ActionFlagComponent flags) && flags.Get(ActionFlag.SkillPreview)
        )
        {
            return;
        }

        // If stuck in CastingSkill state, force reset to Idle
        if (state.CurrentState == CombatState.CastingSkill)
        {
            if (Time.time - state.LastActionTime > 2f)
            {
                state.CurrentState = CombatState.Idle;
                state.LastActionTime = Time.time;
            }
            else
            {
                return;
            }
        }

        // Ensure we have a valid attack direction
        Vector3 attackDir = attack.AttackDirection;

        if (attackDir.sqrMagnitude < 0.0001f)
        {
            if (_world.Components.TryGet(@event.Entity, out TransformComponent transform))
            {
                attackDir = transform.Rotation * Vector3.forward;
            }
            else
            {
                var registry = _world.Services.Resolve<EntityViewRegistry>();
                if (registry.TryGet(@event.Entity, out EntityView view))
                {
                    attackDir = view.transform.forward;
                }
            }
        }

        attackDir.y = 0f;

        if (attackDir.sqrMagnitude < 0.0001f)
        {
            attackDir = Vector3.forward;
        }

        attack.AttackDirection = attackDir.normalized;

        // Switch Combat State (server authoritative)
        _world.Events.Publish(
            new EnterCombatStateEvent { Entity = @event.Entity, TargetState = CombatState.Attacking }
        );

        attack.LastAttackTime = Time.time;
    }

    public void FixedUpdate(float dt) { }

    public void Shutdown()
    {
        _world.Events.Unsubscribe<AttackPressedInputEvent>(OnAttackRequest);
        _world.Events.Unsubscribe<AnimationEventRelayEvent>(OnAnimationRelayEvent);
    }

    private void OnAnimationRelayEvent(AnimationEventRelayEvent @event)
    {
        switch (@event.EventType)
        {
            case AnimationEventRelayType.ATTACK_HIT:
                break;

            case AnimationEventRelayType.ATTACK_END:
                if (_world.Components.TryGet(@event.Entity, out AttackDataComponent attack))
                {
                    attack.IsAttacking = false;
                }

                if (_world.Components.TryGet(@event.Entity, out CombatStateComponent state))
                {
                    state.CurrentState = CombatState.Idle;
                }
                break;
        }
    }
}
