using System;
using Unity.Netcode;
using Unity.Services.Lobbies.Models;
using Unity.VisualScripting;
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

        // Track attacker for kill counts
        if (!@event.Attacker.Equals(default))
        {
            if (_world.Components.Has<EnemyComponent>(@event.Target))
            {
                var enemy = _world.Components.Get<EnemyComponent>(@event.Target);
                enemy.LastAttacker = @event.Attacker;
                enemy.LastDamageTime = Time.time;
            }
            else if (_world.Components.Has<PlayerTagComponent>(@event.Target))
            {
                if (!_world.Components.TryGet(@event.Target, out PlayerScoreComponent score))
                {
                    score = new PlayerScoreComponent();
                    _world.Components.Add(@event.Target, score);
                }

                score.LastAttacker = @event.Attacker;
            }
        }

        _world.Events.Publish(new HealthChangedEvent(@event.Target, health.CurrentHealth, health.MaxHealth));

        // Broadcast damage visual to all clients
        var registry = _world.Services.Resolve<EntityViewRegistry>();
        Vector3 hitPoint = Vector3.zero;

        if (registry.TryGet(@event.Target, out EntityView view))
        {
            hitPoint = view.transform.position;
        }

        // Check if it's a player or enemy
        if (_world.Components.Has<PlayerTagComponent>(@event.Target))
        {
            if (_world.Components.TryGet(@event.Target, out NetworkSyncComponent sync))
            {
                sync.SyncView.BroadcastDamageVisualClientRpc(actualDamage, hitPoint);
            }
        }
        else if (_world.Components.Has<EnemyComponent>(@event.Target))
        {
            if (_world.Components.TryGet(@event.Target, out NetworkObjectComponent netObj))
            {
                var enemySync = netObj.NetworkObject.GetComponent<EnemyNetworkSyncView>();
                if (enemySync != null)
                {
                    enemySync.BroadcastDamageVisualClientRpc(actualDamage, hitPoint);
                }
            }
        }
    }

    public void Shutdown()
    {
        _world.Events.Unsubscribe<DamageEvent>(OnDamage);
    }
}
