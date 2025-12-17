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
            case AnimationEventRelayType.SKILL_END:
                HandleSkillEnd();
                break;
        }
    }

    private void HandleAttackHit()
    {
        Debug.Log($"[AnimationEventRelayView] HandleAttackHit called for entity {_entityView?.EntityInstance.Id}");
        
        if (!_world.Components.TryGet(_entityView.EntityInstance, out AttackDataComponent attack))
        {
            Debug.LogError($"[AnimationEventRelayView] Missing AttackDataComponent for entity {_entityView?.EntityInstance.Id}");
            return;
        }

        if (!_world.Components.TryGet(_entityView.EntityInstance, out WeaponDataComponent weapon))
        {
            Debug.LogError("Missing WeaponDataComponent");
            return;
        }

        // Play attack sound - simplified!
        // System automatically determines if this is Player or Enemy
        if (weapon.AttackSound != null)
        {
            // Direct sound play with automatic category detection
            var category = _world.Components.Has<PlayerTagComponent>(_entityView.EntityInstance)
                ? AudioCategory.Player
                : AudioCategory.Enemy;

            AudioHelper.PlaySound3D(_world, weapon.AttackSound, category, _entityView.transform.position);
        }
        else
        {
            // Fallback to profile-based sound
            _world.Events.Publish(
                new AudioCueEvent(_entityView.EntityInstance, SoundType.Attack, _entityView.transform.position)
            );
        }

        // Trigger attack execution
        Debug.Log($"[AnimationEventRelayView] Publishing AttackExecutionRequestEvent for entity {_entityView.EntityInstance.Id}, Type: {weapon.ExecutionType}");
        
        _world.Events.Publish(
            new AttackExecutionRequestEvent
            {
                Attacker = _entityView.EntityInstance,
                Type = weapon.ExecutionType,
                Direction = attack.AttackDirection,
                Range = weapon.BaseRange,
                Damage = weapon.BaseDamage,
                ImpactEffect = weapon.HitImpactParticlePrefab,
                ProjectilePrefab = weapon.ProjectilePrefab,
                ProjectileSpeed = weapon.ProjectileSpeed,
                ProjectileLifetime = weapon.ProjectileLifetime,
                SpawnOffset = weapon.ProjectileSpawnOffset,
                AreaRadius = weapon.AreaRadius,
                AreaDuration = weapon.AreaDuration,
            }
        );
    }

    private void HandleSkillHit()
    {
        if (!_world.Components.TryGet(_entityView.EntityInstance, out SkillCastBufferComponent skillBuffer))
        {
            return;
        }

        _world.Events.Publish(
            new SkillConfirmExecutionEvent
            {
                Caster = _entityView.EntityInstance,
                Skill = skillBuffer.Skill,
                TargetPoint = skillBuffer.TargetPoint,
                Direction = skillBuffer.Direction,
            }
        );

        skillBuffer.Skill = null;
        skillBuffer.TargetPoint = Vector3.zero;
        skillBuffer.Direction = Vector3.forward;
    }

    private void HandleAttackEnd()
    {
        if (_world.Components.TryGet(_entityView.EntityInstance, out AttackDataComponent attack))
        {
            attack.IsAttacking = false;
        }

        _world.Events.Publish(
            new AnimationEventRelayEvent(_entityView.EntityInstance, AnimationEventRelayType.ATTACK_END)
        );
    }

    private void HandleSkillEnd()
    {
        _world.Events.Publish(
            new AnimationEventRelayEvent(_entityView.EntityInstance, AnimationEventRelayType.SKILL_END)
        );
    }

    public void OnFootstep()
    {
        if (_world == null)
        {
            return;
        }

        // Simple footstep event - system handles Player vs Enemy automatically
        _world.Events.Publish(
            new AudioCueEvent(_entityView.EntityInstance, SoundType.Footstep, _entityView.transform.position)
        );
    }
}
