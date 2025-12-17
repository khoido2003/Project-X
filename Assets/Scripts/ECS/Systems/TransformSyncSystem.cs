using Unity.Netcode;
using UnityEngine;

public class TransformSyncSystem : ISystem
{
    private World _world;

    public void Initialize(World world) => _world = world;

    public void Update(float dt)
    {
        bool isServer = NetworkManager.Singleton?.IsServer == true;
        
        foreach (var (entity, trans) in _world.Components.Query<TransformComponent>())
        {
            // Skip enemies - they have their own sync system
            if (_world.Components.Has<EnemyComponent>(entity))
            {
                continue;
            }

            var registry = _world.Services.Resolve<EntityViewRegistry>();
            if (!registry.TryGet(entity, out EntityView view))
                continue;

            if (isServer)
            {
                // SERVER: Sync FROM Unity Transform TO ECS TransformComponent
                // CharacterController moves the Unity transform, we read it here
                trans.Position = view.transform.position;
                trans.Rotation = view.transform.rotation;
            }
            else
            {
                // CLIENT: Sync FROM ECS TransformComponent TO Unity Transform
                // NetworkSyncView updates the ECS component from NetworkVariables
                // We apply it to the visual here
                view.transform.position = trans.Position;
                
                // CRITICAL: Skip rotation for local player - LookAtMouseView handles it locally
                // for responsive mouse aiming
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
