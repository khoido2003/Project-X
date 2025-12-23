using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class StunSystem : ISystem
{
    private World _world;
    private Dictionary<EntityId, float> _stunEndTimes = new();

    public void Initialize(World world)
    {
        _world = world;
        _world.Events.Subscribe<StunEvent>(OnStunEvent);
    }

    public void Update(float dt)
    {
        if (!NetworkManager.Singleton.IsServer)
        {
            return;
        }

        List<EntityId> toRemove = new();

        foreach (var kvp in _stunEndTimes)
        {
            if (Time.time >= kvp.Value)
            {
                toRemove.Add(kvp.Key);

                if (_world.Components.TryGet(kvp.Key, out MovementDataComponent movement))
                {
                    movement.IsStunned = false;
                }
            }
        }

        foreach (var entity in toRemove)
        {
            _stunEndTimes.Remove(entity);
        }
    }

    public void FixedUpdate(float dt) { }

    private void OnStunEvent(StunEvent @event)
    {
        if (!NetworkManager.Singleton.IsServer)
        {
            return;
        }

        if (_world.Components.TryGet(@event.Target, out MovementDataComponent movement))
        {
            movement.IsStunned = true;
            _stunEndTimes[@event.Target] = Time.time + @event.Duration;
        }

        // Broadcast to all clients
        if (_world.Components.TryGet(@event.Target, out NetworkSyncComponent sync))
        {
            sync.SyncView.BroadcastStunClientRpc(@event.Duration);
        }
    }

    public void Shutdown()
    {
        _world.Events.Unsubscribe<StunEvent>(OnStunEvent);
        _stunEndTimes.Clear();
    }
}
