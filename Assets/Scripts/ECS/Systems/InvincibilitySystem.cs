using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Server-side system to manage invincibility timers.
/// Counts down active invincibility durations and broadcasts end events.
/// </summary>
public class InvincibilitySystem : ISystem
{
    private World _world;

    public void Initialize(World world)
    {
        _world = world;
    }

    public void Update(float dt)
    {
        // Only server manages invincibility timers
        if (!NetworkManager.Singleton.IsServer)
        {
            return;
        }

        foreach (var (entity, invincibility, health) in 
            _world.Components.Query<InvincibilityComponent, HealthDataComponent>())
        {
            if (!invincibility.IsActive)
            {
                continue;
            }

            invincibility.RemainingDuration -= dt;

            if (invincibility.RemainingDuration <= 0f)
            {
                EndInvincibility(entity, invincibility, health);
            }
        }
    }

    public void FixedUpdate(float dt) { }

    private void EndInvincibility(EntityId entity, InvincibilityComponent invincibility, HealthDataComponent health)
    {
        invincibility.IsActive = false;
        invincibility.RemainingDuration = 0f;
        health.IsInvincible = false;

        Debug.Log($"[InvincibilitySystem] Invincibility ended for entity {entity.Id}");

        // Publish end event for visual feedback
        _world.Events.Publish(new InvincibilityEndEvent(entity));

        // Broadcast to clients
        if (_world.Components.TryGet(entity, out NetworkSyncComponent sync))
        {
            sync.SyncView.BroadcastInvincibilityEndClientRpc();
        }
    }

    public void Shutdown() { }
}
