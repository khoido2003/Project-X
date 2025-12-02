using System;
using Unity.Netcode;
using UnityEngine;

public class KnockbackSystem : ISystem
{
    private World _world;

    public void Initialize(World world)
    {
        _world = world;
        _world.Events.Subscribe<KnockbackEvent>(OnKnockBack);
    }

    public void Update(float dt) { }

    public void FixedUpdate(float dt) { }

    private void OnKnockBack(KnockbackEvent @event)
    {
        if (!NetworkManager.Singleton.IsServer)
        {
            return;
        }
        if (!_world.Components.TryGet(@event.Target, out TransformComponent transform))
        {
            return;
        }

        Vector3 knockback = @event.Direction.normalized * @event.Force;

        transform.Position += knockback * Time.deltaTime;

        var registry = _world.Services.Resolve<EntityViewRegistry>();
        if (registry.TryGet(@event.Target, out EntityView view))
        {
            view.transform.position = transform.Position;
        }

        if (_world.Components.TryGet(@event.Target, out NetworkSyncComponent sync))
        {
            sync.SyncView.BroadcastKnockbackClientRpc(@event.Direction, @event.Force);
        }
    }

    public void Shutdown()
    {
        _world.Events.Unsubscribe<KnockbackEvent>(OnKnockBack);
    }
}
