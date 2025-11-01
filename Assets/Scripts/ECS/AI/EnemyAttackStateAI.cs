using UnityEngine;
using UnityEngine.UI;

public class EnemyAttackStateAI : IEnemyState
{
    public EnemyState StateType => EnemyState.Attack;

    private readonly float rotateSpeed = 70f;

    public void OnEnter(World world, EntityId entity)
    {
        var enemy = world.Components.Get<EnemyComponent>(entity);
        enemy.StateTime = enemy.AttackCooldown;
    }

    public void OnUpdate(World world, EntityId entity, float dt)
    {
        var enemy = world.Components.Get<EnemyComponent>(entity);
        enemy.StateTime += dt;

        if (enemy.TargetEntity.Equals(default))
        {
            EnemyAIHelpers.ChangeState(world, entity, EnemyState.Patrol);
            return;
        }

        TransformComponent enemyTransform = world.Components.Get<TransformComponent>(entity);
        TransformComponent targetTransform = world.Components.Get<TransformComponent>(enemy.TargetEntity);

        float dist = Vector3.Distance(targetTransform.Position, enemyTransform.Position);

        if (dist > enemy.AttackRange * 1.2f)
        {
            EnemyAIHelpers.ChangeState(world, entity, EnemyState.Chase);
            return;
        }

        Vector3 dir = (targetTransform.Position - enemyTransform.Position).normalized;

        if (dir.sqrMagnitude > 0.0001f)
        {
            enemyTransform.Rotation = Quaternion.Slerp(
                enemyTransform.Rotation,
                Quaternion.LookRotation(dir),
                dt * rotateSpeed
            );
        }

        if (enemy.StateTime >= enemy.AttackCooldown)
        {
            enemy.StateTime = 0f;

            if (world.Components.TryGet(enemy.TargetEntity, out TransformComponent targetTf))
            {
                if (world.Components.TryGet(entity, out AttackDataComponent attack))
                {
                    Vector3 direction = (
                        targetTf.Position - world.Components.Get<TransformComponent>(entity).Position
                    ).normalized;
                    attack.AttackDirection = direction;
                }
            }
            PerformAttack(world, entity);
        }

        // Take cover when player get close
        if (Time.time - enemy.LastCoverTime > enemy.CoverCooldown)
        {
            if (dist < enemy.AttackRange * 0.7f)
            {
                EnemyAIHelpers.ChangeState(world, entity, EnemyState.TakeCover);
                return;
            }
        }
    }

    private void PerformAttack(World world, EntityId entity)
    {
        if (world.Components.TryGet(entity, out AnimationDataComponent animation))
        {
            world.Events.Publish(
                new AnimationParameterEvent(entity, animation.AttackTrigger, AnimationParameterType.Trigger, null)
            );
        }
    }

    public void OnExit(World world, EntityId entity) { }
}
