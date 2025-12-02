using System;
using Unity.Netcode;
using UnityEngine;
using WebSocketSharp;

public class DamageSystem : ISystem
{
    private World _world;

    public void Initialize(World world)
    {
        _world = world;
        _world.Events.Subscribe<DamageEvent>(OnDamage);
    }

    public void Update(float dt) { }

    public void FixedUpdate(float dt) { }

    private void OnDamage(DamageEvent @event)
    {
        if (!NetworkManager.Singleton.IsServer)
        {
            return;
        }

        if (!_world.Components.TryGet(@event.Target, out HealthDataComponent health))
        {
            Debug.LogWarning($"[DamageSystem] Target {@event.Target} missing HealthDataComponent");
            return;
        }

        if (health.IsDead)
        {
            return;
        }

        float actualDamage = @event.Amount;
        health.CurrentHealth -= actualDamage;

        if (health.CurrentHealth < 0)
        {
            health.CurrentHealth = 0;
        }

        _world.Components.Add(@event.Target, health);

        _world.Events.Publish(new HealthChangedEvent(@event.Target, health.CurrentHealth, health.MaxHealth));

        // Broadcast damage visual to all clients
        if (_world.Components.TryGet(@event.Target, out NetworkSyncComponent sync))
        {
            var registry = _world.Services.Resolve<EntityViewRegistry>();
            Vector3 hitpoint = Vector3.zero;

            if (registry.TryGet(@event.Target, out EntityView view))
            {
                hitpoint = view.transform.position;
            }

            sync.SyncView.BroadcastDamageVisualClientRpc(actualDamage, hitPoint);
        }
    }

    public void Shutdown()
    {
        _world.Events.Unsubscribe<DamageEvent>(OnDamage);
    }
}
