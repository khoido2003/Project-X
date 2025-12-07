using Unity.Netcode;
using UnityEngine;

public class TransformSyncView : EntityView
{
    private Transform _transform;
    private World _world;
    private TransformComponent _transformComponent;

    private EntityId _entity;

    public override void Bind(World world, EntityId entity)
    {
        base.Bind(world, entity);

        _world = world;
        _transform = transform;
        _entity = entity;

        _transformComponent = new TransformComponent(_transform.position, _transform.rotation);
        _world.Components.Add(entity, _transformComponent);
    }

    private void LateUpdate()
    {
        if (_world == null)
        {
            return;
        }
        if (_world.Components.Has<EnemyComponent>(_entity))
        {
            return; // Skip enemies entirely
        }
        if (_world.Components.TryGet(_entity, out NetworkOwnerComponent owner) && owner.IsLocalPlayer)
        {
            // Sync Unity → ECS
            _transformComponent.Position = _transform.position;
            _transformComponent.Rotation = _transform.rotation;
        }
        else
        {
            //ECS → Unity (network state)
            _transform.position = _transformComponent.Position;
            _transform.rotation = _transformComponent.Rotation;
        }
    }
}
