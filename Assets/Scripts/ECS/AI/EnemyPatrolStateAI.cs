using UnityEngine;

public class EnemyPatrolStateAI : IEnemyState
{
    public EnemyState StateType => EnemyState.Patrol;

    public void OnEnter(World world, EntityId entity)
    {
        EnemyComponent enemy = world.Components.Get<EnemyComponent>(entity);
        enemy.StateTime = 0f;

        if (enemy.PatrolWaypoints.Count == 0)
        {
            return;
        }

        RequestPath(world, entity, enemy.PatrolWaypoints[enemy.PatrolIndex]);
    }

    public void OnUpdate(World world, EntityId entity, float dt)
    {
        EnemyComponent enemy = world.Components.Get<EnemyComponent>(entity);

        enemy.StateTime += dt;

        if (!enemy.TargetEntity.Equals(default))
        {
            EnemyAIHelpers.ChangeState(world, entity, EnemyState.Chase);
            return;
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
        }
    }

    private void RequestPath(World world, EntityId entity, Vector3 target)
    {
        world.Events.Publish(new EnemyPathRequestEvent(entity, target, 0.5f));
    }

    public void OnExit(World world, EntityId entity) { }
}
