using Unity.Netcode;
using UnityEngine;

public class TransformSyncSystem : ISystem
{
    private World _world;

    public void Initialize(World world) => _world = world;

    public void Update(float dt)
    {
        foreach (var (entity, trans) in _world.Components.Query<TransformComponent>())
        {
            var registry = _world.Services.Resolve<EntityViewRegistry>();
            if (!registry.TryGet(entity, out EntityView view))
                continue;

            // For enemies: DON'T sync here - EnemyNetworkSyncView handles it
            if (_world.Components.Has<EnemyComponent>(entity))
            {
                continue;
            }

            // Players and other entities
            trans.Position = view.transform.position;
            trans.Rotation = view.transform.rotation;
        }
    }

    public void FixedUpdate(float dt) { }

    public void Shutdown() { }
}
