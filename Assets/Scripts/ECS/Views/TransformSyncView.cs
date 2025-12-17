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

        bool isServer = NetworkManager.Singleton?.IsServer == true;
        bool isLocalPlayer = _world.Components.TryGet(_entity, out NetworkOwnerComponent owner) && owner.IsLocalPlayer;

        if (isServer)
        {
            // SERVER: Write from Unity Transform to ECS (CharacterController moves Unity Transform)
            trans.Position = _transform.position;
            trans.Rotation = _transform.rotation;
        }
        else
        {
            // CLIENT: Read from ECS and apply to Unity Transform
            // NetworkSyncView updates ECS from NetworkVariables

            if (isLocalPlayer)
            {
                // LOCAL PLAYER on CLIENT:
                // - Position comes from server (ECS) since movement is server-authoritative
                // - Rotation is LOCAL (handled by LookAtMouseView for responsive mouse look)
                _transform.position = trans.Position;
                // DON'T overwrite rotation - LookAtMouseView handles it locally
            }
            else
            {
                // REMOTE PLAYERS on CLIENT:
                // - Both position and rotation come from server (ECS)
                _transform.position = trans.Position;
                _transform.rotation = trans.Rotation;
            }
        }
    }
}
