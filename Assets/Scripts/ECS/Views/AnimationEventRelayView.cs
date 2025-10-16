using UnityEngine;

public class AnimationEventRelayView : EntityView
{
    private EntityView _entityView;
    private World _world;

    public override void Bind(World world, EntityId entity)
    {
        base.Bind(world, entity);
        _entityView = GetComponentInParent<EntityView>();
        _world = _entityView.WorldInstance;
    }

    public void OnAnimationTrigger(AnimationEventRelayType eventRelayType)
    {
        if (_world == null)
        {
            Debug.LogError("[AnimationEventRelayView] No World reference found!");
            return;
        }

        switch (eventRelayType)
        {
            case AnimationEventRelayType.ATTACK_HIT:
                HandleAttackHit();
                break;

            case AnimationEventRelayType.SKILL_HIT:
                HandleSkillHit();
                break;

            case AnimationEventRelayType.ATTACK_END:
                HandleAttackEnd();
                break;

            default:
                break;
        }
    }

    private void HandleAttackHit()
    {
        if (!_world.Components.TryGet(_entityView.EntityInstance, out AttackDataComponent attack))
            return;

        if (!_world.Components.TryGet(_entityView.EntityInstance, out WeaponDataComponent weapon))
            return;

        _world.Events.Publish(
            new AttackExecutionRequestEvent
            {
                Attacker = _entityView.EntityInstance,
                Type = weapon.ExecutionType,
                Direction = attack.AttackDirection,
                Range = weapon.BaseRange,
                Damage = weapon.BaseDamage,
                ProjectilePrefab = weapon.ProjectilePrefab,
                ImpactEffect = weapon.HitImpactParticlePrefab,
            }
        );
    }

    private void HandleSkillHit() { }

    private void HandleAttackEnd()
    {
        _world.Events.Publish(
            new AnimationEventRelayEvent(_entityView.EntityInstance, AnimationEventRelayType.ATTACK_END)
        );
    }
}
