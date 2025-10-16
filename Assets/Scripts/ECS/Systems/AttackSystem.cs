using UnityEngine;

public class AttackSystem : ISystem
{
    private World _world;

    public void Initialize(World world)
    {
        _world = world;
        _world.Events.Subscribe<AttackInputEvent>(OnAttack);
        _world.Events.Subscribe<AnimationEventRelayEvent>(OnAnimationRelayEvent);
    }

    public void Update(float dt) { }

    private void OnAttack(AttackInputEvent @event)
    {
        if (SkillPreviewView.IsPreviewActive)
            return;

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

        if (state.CurrentState != CombatState.Idle)
        {
            return;
        }

        if (!attack.CanAttack(weapon.BaseCooldown) || attack.IsAttacking)
        {
            return;
        }

        state.CurrentState = CombatState.Attacking;
        state.LastActionTime = Time.time;

        attack.IsAttacking = true;
        attack.LastAttackTime = Time.time;
        attack.AttackDirection = Vector3.forward;

        int randomIndex = Random.Range(0, weapon.TotalAttackAnimations);

        ////////////////////////////////////////////////////////

        // Publish event

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

        // GAMEPLAY LOGIC
        _world.Events.Publish(new AttackStartedEvent(@event.Entity, randomIndex));
    }

    public void FixedUpdate(float dt) { }

    public void Shutdown()
    {
        _world.Events.Unsubscribe<AttackInputEvent>(OnAttack);
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
