using UnityEngine;

public class EnemyDeadStateAI : IEnemyState
{
    public EnemyState StateType => EnemyState.Dead;

    public void OnEnter(World world, EntityId entity)
    {
        var registry = world.Services.Resolve<EntityViewRegistry>();
        if (registry.TryGet(entity, out EntityView view))
        {
            Object.Destroy(view.gameObject);
        }

        // Optionally remove entity from ECS
        world.Entities.DestroyEntity(entity);
    }

    public void OnUpdate(World world, EntityId entity, float dt) { }

    public void OnExit(World world, EntityId entity) { }
}
