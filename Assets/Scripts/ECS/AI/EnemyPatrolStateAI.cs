using System.IO;
using UnityEngine;

public class EnemyPatrolStateAI : IEnemyState
{
    public EnemyState StateType => EnemyState.Patrol;

    public void OnEnter(World world, EntityId entity)
    {
        EnemyComponent enemy = world.Components.Get<EnemyComponent>(entity);

        enemy.StateTime = 0f;
        enemy.WaypointIndex = 0;

        if (enemy.PatrolWaypoints.Count == 0)
        {
            EnemyAIHelpers.ChangeState(world, entity, EnemyState.Idle);
            return;
        }

        if (world.Components.TryGet(entity, out AnimationDataComponent anim))
        {
            world.Events.Publish(
                new AnimationParameterEvent(entity, anim.IsMovingParam, AnimationParameterType.Bool, true)
            );
            world.Events.Publish(
                new AnimationParameterEvent(entity, anim.IsRunningParam, AnimationParameterType.Bool, false)
            );
        }

        RequestPath(world, entity, enemy.PatrolWaypoints[enemy.PatrolIndex]);
    }

    public void OnUpdate(World world, EntityId entity, float dt)
    {
        EnemyComponent enemy = world.Components.Get<EnemyComponent>(entity);
        WeaponDataComponent weapon = world.Components.Get<WeaponDataComponent>(entity);

        enemy.StateTime += dt;

        if (!enemy.TargetEntity.Equals(default))
        {
            EnemyAIHelpers.ChangeState(world, entity, EnemyState.Chase);
            return;
        }

        if (world.Components.TryGet(enemy.TargetEntity, out TransformComponent targetTf))
        {
            TransformComponent enemyTf = world.Components.Get<TransformComponent>(entity);

            float distance = Vector3.Distance(targetTf.Position, enemyTf.Position);

            if (distance < weapon.BaseRange)
            {
                EnemyAIHelpers.ChangeState(world, entity, EnemyState.Attack);
                return;
            }
            else
            {
                EnemyAIHelpers.ChangeState(world, entity, EnemyState.Chase);
            }
        }

        if (enemy.StateTime >= enemy.PatrolDuration)
        {
            EnemyAIHelpers.ChangeState(world, entity, EnemyState.Idle);
            return;
        }

        if (!enemy.HasPath || enemy.WaypointIndex >= enemy.Path.Count)
        {
            enemy.PatrolIndex = (enemy.PatrolIndex + 1) % enemy.PatrolWaypoints.Count;

            RequestPath(world, entity, enemy.PatrolWaypoints[enemy.PatrolIndex]);

            if (world.Components.TryGet(entity, out AnimationDataComponent anim))
            {
                world.Events.Publish(
                    new AnimationParameterEvent(entity, anim.IsMovingParam, AnimationParameterType.Bool, true)
                );
            }
        }
    }

    private void RequestPath(World world, EntityId entity, Vector3 target)
    {
        world.Events.Publish(new EnemyPathRequestEvent(entity, target, 0.5f));
    }

    public void OnExit(World world, EntityId entity)
    {
        AnimationDataComponent animation = world.Components.Get<AnimationDataComponent>(entity);

        world.Events.Publish(
            new AnimationParameterEvent(entity, animation.IsMovingParam, AnimationParameterType.Bool, false)
        );
    }
}
