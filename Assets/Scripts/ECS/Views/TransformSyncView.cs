using UnityEngine;

public class TransformSyncView : EntityView
{
    private Transform _transform;
    private World _world;
    private TransformComponent _transformComponent;

    public override void Bind(World world, EntityId entity)
    {
        base.Bind(world, entity);

        _world = world;
        _transform = transform;

        _transformComponent = new TransformComponent(_transform.position, _transform.rotation);
        _world.Components.Add(entity, _transformComponent);
    }

    private void LateUpdate()
    {
        if (_world == null)
            return;

        // Sync Unity → ECS
        _transformComponent.Position = _transform.position;
        _transformComponent.Rotation = _transform.rotation;
    }
}
