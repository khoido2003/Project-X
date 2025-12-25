using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Syncs Transform for PLAYERS only.
/// Enemies use EnemyNetworkSyncView for their transform sync.
/// </summary>
public class TransformSyncView : EntityView
{
    private Transform _transform;
    private World _world;
    private EntityId _entity;
    private bool _isEnemy;

    public override void Bind(World world, EntityId entity)
    {
        base.Bind(world, entity);

        _world = world;
        _transform = transform;
        _entity = entity;
        
        // Check if this is an enemy - if so, disable this script
        // Enemies use EnemyNetworkSyncView for transform sync
        _isEnemy = world.Components.Has<EnemyComponent>(entity);
        if (_isEnemy)
        {
            enabled = false;
            return;
        }

        if (!_world.Components.Has<TransformComponent>(_entity))
        {
            _world.Components.Add(entity, new TransformComponent(_transform.position, _transform.rotation));
        }
    }

    private void LateUpdate()
    {
        if (_world == null || _isEnemy)
            return;

        if (!_world.Components.TryGet(_entity, out TransformComponent trans))
            return;

        bool isServer = NetworkManager.Singleton?.IsServer == true;
        bool isLocalPlayer = _world.Components.TryGet(_entity, out NetworkOwnerComponent owner) && owner.IsLocalPlayer;

        if (isServer)
        {
            // SERVER: Write from Unity Transform to ECS
            trans.Position = _transform.position;
            trans.Rotation = _transform.rotation;
        }
        else
        {
            // CLIENT: Read from ECS and apply to Unity Transform
            if (isLocalPlayer)
            {
                _transform.position = trans.Position;
                // DON'T overwrite rotation - LookAtMouseView handles it locally
            }
            else
            {
                _transform.position = trans.Position;
                _transform.rotation = trans.Rotation;
            }
        }
    }
}

