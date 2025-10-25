using UnityEngine;

public class EnemyAttackStateAI : IEnemyState
{
    public EnemyState StateType => EnemyState.Attack;

    public void OnEnter(World world, EntityId entity)
    {
        var enemy = world.Components.Get<EnemyComponent>(entity);
        enemy.StateTime = 0f;
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

        EntityViewRegistry registry = world.Services.Resolve<EntityViewRegistry>();
        Vector3 targetPos;
        if (!registry.TryGet(entity, out EntityView targetView))
        {
            EnemyAIHelpers.ChangeState(world, entity, EnemyState.Patrol);
            return;
        }

        targetPos = targetView.transform.position;

        float distance = Vector3.Distance(world.Components.Get<TransformComponent>(entity).Position, targetPos);

        if (distance > enemy.AttackRange * 1.2f)
        {
            EnemyAIHelpers.ChangeState(world, entity, EnemyState.Chase);
            return;
        }

        if (enemy.StateTime >= enemy.AttackCooldown)
        {
            enemy.StateTime = 0f;
            Attack(world, entity, enemy);
        }
    }

    private void Attack(World world, EntityId entity, EnemyComponent enemy)
    {
        if (enemy.IsRanged)
        {
            world.Events.Publish(
                new AttackExecutionRequestEvent
                {
                    Attacker = entity,
                    Damage = enemy.Damage,
                    Range = enemy.AttackRange,
                    Type = AttackExecutionType.Projectile,
                }
            );
        }
        else
        {
            world.Events.Publish(
                new AttackExecutionRequestEvent
                {
                    Attacker = entity,
                    Damage = enemy.Damage,
                    Range = enemy.AttackRange,
                    Type = AttackExecutionType.Melee,
                }
            );
        }
    }

    public void OnExit(World world, EntityId entity) { }
}
