using UnityEngine;

public class EnemyChaseStateAI : IEnemyState
{
    public EnemyState StateType => EnemyState.Chase;

    public void OnEnter(World world, EntityId entity)
    {
        AnimationDataComponent animation = world.Components.Get<AnimationDataComponent>(entity);

        world.Events.Publish(
            new AnimationParameterEvent(entity, animation.IsRunningParam, AnimationParameterType.Bool, true)
        );

        RequestPathToTarget(world, entity);
    }

    public void OnUpdate(World world, EntityId entity, float dt)
    {
        var enemy = world.Components.Get<EnemyComponent>(entity);
        var weapon = world.Components.Get<WeaponDataComponent>(entity);
        var enemyTf = world.Components.Get<TransformComponent>(entity);
        enemy.StateTime += dt;

        // ALL enemies dynamically switch to nearest player during chase
        // This prevents players from hiding while another is being chased
        UpdateTargetToNearest(world, entity, enemy, enemyTf);

        if (enemy.TargetEntity.Equals(default))
        {
            EnemyAIHelpers.ChangeState(world, entity, EnemyState.Patrol);
            return;
        }

        EntityViewRegistry registry = world.Services.Resolve<EntityViewRegistry>();
        if (!registry.TryGet(enemy.TargetEntity, out EntityView targetView))
        {
            EnemyAIHelpers.ChangeState(world, entity, EnemyState.Patrol);
            return;
        }

        // Drop target if player becomes untargetable (e.g., cloaked) or dead
        if (world.Components.TryGet(enemy.TargetEntity, out HealthDataComponent targetHealth))
        {
            if (targetHealth.IsUntargetable || targetHealth.IsDead)
            {
                enemy.TargetEntity = default;
                EnemyAIHelpers.ChangeState(world, entity, EnemyState.Patrol);
                return;
            }
        }

        Vector3 targetPos = targetView.transform.position;

        float distance = Vector3.Distance(targetPos, enemyTf.Position);

        // Switch to attack if near player
        if (enemy.IsRanged)
        {
            if (distance <= weapon.BaseRange)
            {
                EnemyAIHelpers.ChangeState(world, entity, EnemyState.Attack);
                return;
            }
        }
        else
        {
            if (distance <= weapon.BaseRange)
            {
                EnemyAIHelpers.ChangeState(world, entity, EnemyState.Attack);
                return;
            }

            // Boss: Check for special moves with smart priority
            if (enemy.IsBoss && world.Components.TryGet(entity, out BossComponent boss))
            {
                // Close range: prefer flamethrower (it's more effective up close)
                if (distance <= boss.FlamethrowerRange && boss.CanFlamethrower)
                {
                    EnemyAIHelpers.ChangeState(world, entity, EnemyState.Flamethrower);
                    return;
                }
                
                // Mid-to-far range: use jump attack to close distance
                if (boss.CanJumpAttack && distance >= boss.JumpAttackMinRange && distance <= boss.JumpAttackRange)
                {
                    EnemyAIHelpers.ChangeState(world, entity, EnemyState.JumpAttack);
                    return;
                }
            }
        }

        if (Time.time - enemy.LastCoverTime > enemy.CoverCooldown)
        {
            if (distance < weapon.BaseRange * 0.7f)
            {
                EnemyAIHelpers.ChangeState(world, entity, EnemyState.TakeCover);
                return;
            }
        }

        // Check if stuck trying to reach current target - switch to alternative if available
        // Uses NoProgressTimer from EnemyMovementSystem to detect stuck state
        // Only switch after 5 seconds of no progress (give time for pathfinding to work)
        if (enemy.NoProgressTimer > 5f)
        {
            if (TrySwitchToAlternativeTarget(world, entity, enemy, enemyTf))
            {
                enemy.NoProgressTimer = 0f; // Reset timer after switching
                RequestPathToTarget(world, entity);
                return;
            }
        }

        if (Time.time - enemy.LastRequestTime > enemy.RequestCooldown)
        {
            RequestPathToTarget(world, entity);
        }
    }
    
    /// <summary>
    /// Tries to switch to a different alive player when stuck reaching current target.
    /// Returns true if successfully switched to a new target.
    /// </summary>
    private bool TrySwitchToAlternativeTarget(World world, EntityId entity, EnemyComponent enemy, TransformComponent enemyTf)
    {
        EntityId currentTarget = enemy.TargetEntity;
        EntityId bestAlternative = default;
        float bestDist = float.MaxValue;
        
        foreach (var (playerEntity, player, playerTf, health) in
            world.Components.Query<PlayerTagComponent, TransformComponent, HealthDataComponent>())
        {
            // Skip dead or untargetable players
            if (health.IsDead || health.IsUntargetable) continue;
            
            // Skip current target (we're already stuck trying to reach them)
            if (playerEntity.Equals(currentTarget)) continue;
            
            float dist = Vector3.Distance(enemyTf.Position, playerTf.Position);
            if (dist < bestDist)
            {
                bestDist = dist;
                bestAlternative = playerEntity;
            }
        }
        
        if (!bestAlternative.Equals(default))
        {
            enemy.TargetEntity = bestAlternative;
            Debug.Log($"[EnemyChaseStateAI] Entity {entity.Id} switched target due to stuck - new target: {bestAlternative.Id}");
            return true;
        }
        
        return false;
    }

    private void RequestPathToTarget(World world, EntityId entity)
    {
        EnemyComponent enemy = world.Components.Get<EnemyComponent>(entity);

        if (enemy.TargetEntity.Equals(default))
        {
            return;
        }

        EntityViewRegistry registry = world.Services.Resolve<EntityViewRegistry>();

        if (!registry.TryGet(enemy.TargetEntity, out EntityView targetView))
        {
            return;
        }

        Vector3 targetPos = targetView.transform.position;
        world.Events.Publish(new EnemyPathRequestEvent(entity, targetPos, enemy.StoppingDistance));

        enemy.LastRequestedTarget = targetPos;
        enemy.LastRequestTime = Time.time;
    }

    public void OnExit(World world, EntityId entity)
    {
        AnimationDataComponent animation = world.Components.Get<AnimationDataComponent>(entity);

        world.Events.Publish(
            new AnimationParameterEvent(entity, animation.IsRunningParam, AnimationParameterType.Bool, false)
        );
    }
    
    /// <summary>
    /// Updates enemy target to the nearest alive player during chase.
    /// All enemies now dynamically switch to closest player to prevent hiding.
    /// </summary>
    private void UpdateTargetToNearest(World world, EntityId entity, EnemyComponent enemy, TransformComponent enemyTf)
    {
        float nearestDist = float.MaxValue;
        EntityId nearestPlayer = default;

        foreach (var (playerEntity, player, playerTf, health) in
            world.Components.Query<PlayerTagComponent, TransformComponent, HealthDataComponent>())
        {
            if (health.IsDead) continue;
            
            // Skip untargetable players (e.g., cloaked Murder Kitten)
            if (health.IsUntargetable) continue;
            
            float dist = Vector3.Distance(enemyTf.Position, playerTf.Position);
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearestPlayer = playerEntity;
            }
        }

        // Switch to nearest player (even if different from current target)
        if (!nearestPlayer.Equals(default) && !nearestPlayer.Equals(enemy.TargetEntity))
        {
            enemy.TargetEntity = nearestPlayer;
            
            // Request new path to new target
            world.Events.Publish(new EnemyPathRequestEvent(entity, 
                world.Components.Get<TransformComponent>(nearestPlayer).Position, 
                enemy.StoppingDistance));
        }
        else if (!nearestPlayer.Equals(default) && enemy.TargetEntity.Equals(default))
        {
            // No current target but found a player - set it
            enemy.TargetEntity = nearestPlayer;
        }
    }
}
