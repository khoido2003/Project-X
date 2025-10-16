using UnityEngine;

public class TransformSyncSystem : ISystem
{
    private World _world;
    private EntityViewRegistry _registry;

    public void Initialize(World world)
    {
        _world = world;
        _registry = _world.Services.Resolve<EntityViewRegistry>();
    }

    public void Update(float dt)
    {
        // foreach (var (entity, transformData) in _world.Components.Query<TransformComponent>())
        // {
        //     if (_registry.TryGet(entity, out EntityView view))
        //     {
        //         Transform tf = view.transform;
        //
        //         // ECS → Unity sync
        //         tf.position = transformData.Position;
        //         tf.rotation = transformData.Rotation;
        //     }
        // }
    }

    public void FixedUpdate(float dt) { }

    public void Shutdown() { }
}
