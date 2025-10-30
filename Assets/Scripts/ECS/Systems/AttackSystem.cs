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
        if (
            _world.Components.TryGet(@event.Entity, out ActionFlagComponent flags) && flags.Get(ActionFlag.SkillPreview)
        )
        {
            return;
        }

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

        if (state.CurrentState == CombatState.CastingSkill)
        {
            return;
        }

        if (!attack.CanAttack(weapon.BaseCooldown) || attack.IsAttacking)
        {
            return;
        }

        // Switch Combat State
        _world.Events.Publish(
            new EnterCombatStateEvent { Entity = @event.Entity, TargetState = CombatState.Attacking }
        );

        attack.IsAttacking = true;
        attack.LastAttackTime = Time.time;
        attack.AttackDirection = Vector3.forward;

        int randomIndex = Random.Range(0, weapon.TotalAttackAnimations);

        ////////////////////////////////////////////////////////

        // ANIMATION
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
