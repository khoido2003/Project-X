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
            
            // Boss: Immediately hunt players - no waiting!
            if (enemy.IsBoss)
            {
                FindAndTargetNearestPlayer(world, entity, enemy);
            }
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

        // Boss: Always actively hunt players
        if (enemy.IsBoss)
        {
            FindAndTargetNearestPlayer(world, entity, enemy);
            if (!enemy.TargetEntity.Equals(default))
            {
                EnemyAIHelpers.ChangeState(world, entity, EnemyState.Chase);
                return;
            }
        }

        if (enemy.StateTime > MAX_IDLE_TIME && enemy.PatrolWaypoints.Count > 0)
        {
            EnemyAIHelpers.ChangeState(world, entity, EnemyState.Patrol);
        }
    }

    private void FindAndTargetNearestPlayer(World world, EntityId entity, EnemyComponent enemy)
    {
        var enemyTf = world.Components.Get<TransformComponent>(entity);
        float nearestDist = float.MaxValue;
        EntityId nearestPlayer = default;

        foreach (var (playerEntity, player, playerTf, health) in
            world.Components.Query<PlayerTagComponent, TransformComponent, HealthDataComponent>())
        {
            if (health.IsDead) continue;
            
            float dist = Vector3.Distance(enemyTf.Position, playerTf.Position);
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearestPlayer = playerEntity;
            }
        }

        if (!nearestPlayer.Equals(default))
        {
            enemy.TargetEntity = nearestPlayer;
        }
    }

    public void OnExit(World world, EntityId entity) { }
}
