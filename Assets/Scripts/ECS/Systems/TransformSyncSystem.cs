using Unity.Netcode;
using UnityEngine;

public class TransformSyncSystem : ISystem
{
    private World _world;
    private EntityViewRegistry _registry;

    public void Initialize(World world)
    {
        _world = world;
        _registry = world.Services.Resolve<EntityViewRegistry>();
    }

    public void Update(float dt)
    {
        if (_registry == null)
            return;
            
        bool isServer = NetworkManager.Singleton?.IsServer == true;
        
        foreach (var (entity, trans) in _world.Components.Query<TransformComponent>())
        {
            // Skip enemies - they have their own sync system (EnemyNetworkSyncView)
            if (_world.Components.Has<EnemyComponent>(entity))
            {
                continue;
            }

            if (!_registry.TryGet(entity, out EntityView view))
                continue;

            if (isServer)
            {
                // SERVER: Sync FROM Unity Transform TO ECS TransformComponent
                trans.Position = view.transform.position;
                trans.Rotation = view.transform.rotation;
            }
            else
            {
                // CLIENT: Sync FROM ECS TransformComponent TO Unity Transform
                view.transform.position = trans.Position;
                
                // Skip rotation for local player - LookAtMouseView handles it locally
                bool isLocalPlayer = _world.Components.TryGet(entity, out NetworkOwnerComponent owner) && owner.IsLocalPlayer;
                if (!isLocalPlayer)
                {
                    view.transform.rotation = trans.Rotation;
                }
            }
        }
    }

    public void FixedUpdate(float dt) { }

    public void Shutdown() { }
}

