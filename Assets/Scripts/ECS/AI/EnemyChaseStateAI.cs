using UnityEngine;

public class EnemyChaseStateAI : IEnemyState
{
    public EnemyState StateType => EnemyState.Chase;

    public void OnEnter(World world, EntityId entity)
    {
        RequestPathToTarget(world, entity);
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
        if (!registry.TryGet(enemy.TargetEntity, out EntityView targetView))
        {
            EnemyAIHelpers.ChangeState(world, entity, EnemyState.Patrol);
            return;
        }

        Vector3 targetPos = targetView.transform.position;
        float distance = Vector3.Distance(targetPos, world.Components.Get<TransformComponent>(entity).Position);

        // if (distance <= enemy.AttackRange)
        // {
        //     EnemyAIHelpers.ChangeState(world, entity, EnemyState.Attack);
        //     return;
        // }

        if (Time.time - enemy.LastRequestTime > enemy.RequestCooldown)
        {
            RequestPathToTarget(world, entity);
        }
    }

    private void RequestPathToTarget(World world, EntityId entity)
    {
        EnemyComponent enemy = world.Components.Get<EnemyComponent>(entity);

        if (enemy.TargetEntity.Equals(default))
            return;

        EntityViewRegistry registry = world.Services.Resolve<EntityViewRegistry>();

        if (!registry.TryGet(enemy.TargetEntity, out EntityView targetView))
            return;

        Vector3 targetPos = targetView.transform.position;
        world.Events.Publish(new EnemyPathRequestEvent(entity, targetPos, enemy.StoppingDistance));

        enemy.LastRequestedTarget = targetPos;
        enemy.LastRequestTime = Time.time;
    }

    public void OnExit(World world, EntityId entity) { }
}
