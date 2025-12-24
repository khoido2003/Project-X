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

        // Boss: Dynamically switch to nearest player during chase
        if (enemy.IsBoss)
        {
            UpdateBossTargetToNearest(world, entity, enemy, enemyTf);
        }

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

        if (Time.time - enemy.LastRequestTime > enemy.RequestCooldown)
        {
            RequestPathToTarget(world, entity);
        }
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
    /// Updates boss target to the nearest alive player during chase
    /// </summary>
    private void UpdateBossTargetToNearest(World world, EntityId entity, EnemyComponent enemy, TransformComponent enemyTf)
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

        // Switch to nearest if different from current (and there is a valid target)
        if (!nearestPlayer.Equals(default) && !nearestPlayer.Equals(enemy.TargetEntity))
        {
            enemy.TargetEntity = nearestPlayer;
            
            // Request new path to new target
            world.Events.Publish(new EnemyPathRequestEvent(entity, 
                world.Components.Get<TransformComponent>(nearestPlayer).Position, 
                enemy.StoppingDistance));
        }
    }
}
