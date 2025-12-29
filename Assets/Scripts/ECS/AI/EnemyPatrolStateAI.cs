using System.IO;
using UnityEngine;

public class EnemyPatrolStateAI : IEnemyState
{
    public EnemyState StateType => EnemyState.Patrol;

    public void OnEnter(World world, EntityId entity)
    {
        EnemyComponent enemy = world.Components.Get<EnemyComponent>(entity);
        TransformComponent enemyTf = world.Components.Get<TransformComponent>(entity);

        enemy.StateTime = 0f;
        enemy.WaypointIndex = 0;

        // Only generate spread patrol points when:
        // 1. Enemy had a target previously (was chasing/attacking someone who died)
        // 2. No alive players exist now
        // 3. Game has been running for at least 5 seconds (avoid spreading at game start)
        bool shouldSpread = false;
        if (Time.timeSinceLevelLoad > 5f && enemy.LastAttacker != default)
        {
            bool anyPlayersAlive = false;
            foreach (var (playerEntity, player, health) in 
                world.Components.Query<PlayerTagComponent, HealthDataComponent>())
            {
                if (!health.IsDead && !health.IsUntargetable)
                {
                    anyPlayersAlive = true;
                    break;
                }
            }
            shouldSpread = !anyPlayersAlive;
        }
        
        // If no alive players and enemy was engaged, spread patrol points
        if (shouldSpread)
        {
            GenerateSpreadPatrolPoints(enemy, enemyTf, entity);
        }

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
    
    /// <summary>
    /// Generates spread-out patrol points across the map when no players are alive.
    /// This prevents enemies from clustering in one area and spawn camping.
    /// </summary>
    private void GenerateSpreadPatrolPoints(EnemyComponent enemy, TransformComponent enemyTf, EntityId entity)
    {
        GridSystem grid = GridSystem.Instance;
        if (grid == null) return;
        
        enemy.PatrolWaypoints.Clear();
        enemy.PatrolIndex = 0;
        
        // Use entity ID to seed randomization - each enemy gets different patrol points
        var rng = new System.Random((int)entity.Id + System.DateTime.Now.Millisecond);
        
        // Generate 3-5 patrol points at moderate distances (not too far!)
        int pointCount = rng.Next(3, 6);
        float minRadius = 5f;  // Minimum spread distance
        float maxRadius = 12f; // Maximum spread distance (reduced from 25f)
        
        for (int i = 0; i < pointCount; i++)
        {
            // Generate random angle with offset based on index to spread points evenly
            float baseAngle = (360f / pointCount) * i;
            float randomOffset = (float)(rng.NextDouble() * 60f - 30f); // ±30 degrees
            float angle = (baseAngle + randomOffset) * Mathf.Deg2Rad;
            
            // Random distance between 5-12 units (much closer than before)
            float distance = (float)(rng.NextDouble() * (maxRadius - minRadius) + minRadius);
            
            Vector3 candidate = enemyTf.Position + new Vector3(
                Mathf.Cos(angle) * distance,
                0f,
                Mathf.Sin(angle) * distance
            );
            
            // Snap to walkable grid position
            Vector2Int gridPos = grid.GetGridPosition(candidate);
            gridPos = grid.FindNearestWalkable(gridPos);
            Vector3 worldPos = grid.GetWorldPosition(gridPos);
            worldPos.y = enemyTf.Position.y;
            
            enemy.PatrolWaypoints.Add(worldPos);
        }
    }

    public void OnUpdate(World world, EntityId entity, float dt)
    {
        EnemyComponent enemy = world.Components.Get<EnemyComponent>(entity);
        WeaponDataComponent weapon = world.Components.Get<WeaponDataComponent>(entity);
        TransformComponent enemyTf = world.Components.Get<TransformComponent>(entity);
        
        // ACTIVE HUNTING: Constantly search for players during patrol
        // This ensures enemies immediately detect respawning players
        FindAndTargetNearestPlayer(world, entity, enemy, enemyTf);

        enemy.StateTime += dt;

        if (!enemy.TargetEntity.Equals(default))
        {
            EnemyAIHelpers.ChangeState(world, entity, EnemyState.Chase);
            return;
        }

        if (world.Components.TryGet(enemy.TargetEntity, out TransformComponent targetTf))
        {
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
    
    /// <summary>
    /// Actively searches for and targets the nearest alive player.
    /// Called every frame during patrol to immediately detect respawning players.
    /// </summary>
    private void FindAndTargetNearestPlayer(World world, EntityId entity, EnemyComponent enemy, TransformComponent enemyTf)
    {
        float nearestDist = float.MaxValue;
        EntityId nearestPlayer = default;

        foreach (var (playerEntity, player, playerTf, health) in
            world.Components.Query<PlayerTagComponent, TransformComponent, HealthDataComponent>())
        {
            if (health.IsDead) continue;
            if (health.IsUntargetable) continue;
            
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
            // Target found - will transition to Chase in OnUpdate
        }
    }
}
