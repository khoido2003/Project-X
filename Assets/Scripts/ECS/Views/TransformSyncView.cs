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

        if (!_world.Components.Has<TransformComponent>(_entity))
        {
            _world.Components.Add(entity, new TransformComponent(_transform.position, _transform.rotation));
        }
    }

    private void LateUpdate()
    {
        if (_world == null)
        {
            return;
        }

        if (!_world.Components.TryGet(_entity, out TransformComponent trans))
        {
            return;
        }

        if (_world.Components.TryGet(_entity, out NetworkOwnerComponent owner) && owner.IsLocalPlayer)
        {
            trans.Position = _transform.position;
            trans.Rotation = _transform.rotation;
        }
        else
        {
            _transform.position = trans.Position;
            _transform.rotation = trans.Rotation;
        }
    }
}
