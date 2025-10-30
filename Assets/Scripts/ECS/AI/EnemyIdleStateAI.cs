using UnityEngine;

public class EnemyIdleStateAI : IEnemyState
{
    public EnemyState StateType => EnemyState.Idle;
    private const float MAX_IDLE_TIME = 2f;

    public void OnEnter(World world, EntityId entity)
    {
        if (world.Components.TryGet(entity, out EnemyComponent enemy))
        {
            enemy.StateTime = 0f;
        }
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

        if (enemy.StateTime > MAX_IDLE_TIME && enemy.PatrolWaypoints.Count > 0)
        {
            EnemyAIHelpers.ChangeState(world, entity, EnemyState.Patrol);
        }
    }

    public void OnExit(World world, EntityId entity) { }
}
