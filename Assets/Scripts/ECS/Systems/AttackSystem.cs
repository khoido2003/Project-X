using UnityEngine;

public class AttackSystem : ISystem
{
    private World _world;

    public void Initialize(World world)
    {
        _world = world;
        _world.Events.Subscribe<AttackInputEvent>(OnAttack);
    }

    public void Update(float dt)
    {
        foreach (var (entity, attack) in _world.Components.Query<AttackDataComponent>())
        {
            if (!attack.IsPlayerControlled)
            {
                continue;
            }

            if (!_world.Components.TryGet(entity, out WeaponDataComponent weapon))
            {
                continue;
            }
        }
    }

    private void OnAttack(AttackInputEvent @event)
    {
        if (!_world.Components.TryGet(@event.Entity, out AttackDataComponent attack))
        {
            return;
        }

        if (!_world.Components.TryGet(@event.Entity, out WeaponDataComponent weapon))
        {
            return;
        }

        if (Time.time < attack.LastAttackTime + weapon.WeaponData.attackCooldown)
        {
            return;
        }

        attack.IsAttacking = true;
        attack.LastAttackTime = Time.time;
        attack.AttackDirection = Vector3.forward;

        int randomIndex = Random.Range(0, weapon.WeaponData.totalAttackAnimations);

        ////////////////////////////////////////////////////////

        // Publish event

        // ANIMATION
        _world.Events.Publish(
            new AnimationParameterEvent(@event.Entity, "attackIndex", AnimationParameterType.Float, randomIndex)
        );

        _world.Events.Publish(
            new AnimationParameterEvent(
                @event.Entity,
                weapon.WeaponData.attackAnimationTrigger,
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
    }
}
