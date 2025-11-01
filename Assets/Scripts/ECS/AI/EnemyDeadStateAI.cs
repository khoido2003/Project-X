using UnityEngine;

public class EnemyDeadStateAI : IEnemyState
{
    public EnemyState StateType => EnemyState.Dead;

    private const float DESPAWN_TIME = 3f;

    public void OnEnter(World world, EntityId entity)
    {
        var enemy = world.Components.Get<EnemyComponent>(entity);
        enemy.DeathTimer = 0f;

        if (!enemy.RagdollSpawned)
        {
            var registry = world.Services.Resolve<EntityViewRegistry>();
            if (registry.TryGet(entity, out EntityView view))
            {
                RagdollUtility.ActivateRagdoll(view.GetComponentInChildren<RagdollReference>().gameObject);
            }
            enemy.RagdollSpawned = true;
        }

        if (world.Components.TryGet(entity, out HealthDataComponent health))
        {
            health.IsDead = true;
        }

        enemy.RagdollSpawned = true;
    }

    public void OnUpdate(World world, EntityId entity, float dt)
    {
        var enemy = world.Components.Get<EnemyComponent>(entity);
        enemy.DeathTimer += dt;

        if (enemy.DeathTimer >= DESPAWN_TIME)
        {
            var registry = world.Services.Resolve<EntityViewRegistry>();
            if (registry.TryGet(entity, out EntityView view))
            {
                registry.Unregister(entity);

                Object.Destroy(view.gameObject);
            }

            world.Entities.DestroyEntity(entity);
        }
    }

    public void OnExit(World world, EntityId entity) { }
}
