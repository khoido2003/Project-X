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

        // IMPROVEMENT: ALL enemies actively hunt players after short idle
        // This makes enemies more threatening and responsive
        if (enemy.StateTime > 0.5f)
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
        
        // Boss: Check target switch cooldown to prevent instant target switching
        if (enemy.IsBoss && world.Components.TryGet(entity, out BossComponent boss))
        {
            // If we had a target recently, wait before switching
            if (!boss.LastKnownTarget.Equals(default) && !boss.CanSwitchTarget)
            {
                return; // Still in cooldown, don't switch targets
            }
        }
        
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
            // Boss: Track target switching
            if (enemy.IsBoss && world.Components.TryGet(entity, out BossComponent bossComp))
            {
                if (!nearestPlayer.Equals(bossComp.LastKnownTarget))
                {
                    bossComp.LastTargetSwitchTime = Time.time;
                    bossComp.LastKnownTarget = nearestPlayer;
                }
            }
            enemy.TargetEntity = nearestPlayer;
        }
    }

    public void OnExit(World world, EntityId entity) { }
}
