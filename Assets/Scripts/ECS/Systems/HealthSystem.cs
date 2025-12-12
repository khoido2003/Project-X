using Unity.Netcode;
using UnityEngine;

public class HealthSystem : ISystem
{
    private World _world;

    public void Initialize(World world)
    {
        _world = world;

        if (NetworkManager.Singleton.IsServer)
        {
            foreach (var (entity, health) in _world.Components.Query<HealthDataComponent>())
            {
                world.Events.Publish(new HealthChangedEvent(entity, health.CurrentHealth, health.MaxHealth));
            }
        }
    }

    public void Update(float dt)
    {
        if (!NetworkManager.Singleton.IsServer)
        {
            return;
        }

        foreach (var (entity, health) in _world.Components.Query<HealthDataComponent>())
        {
            if (health.IsDead)
            {
                continue;
            }

            // Health check
            if (health.CurrentHealth <= 0f)
            {
                health.CurrentHealth = 0f;
                health.IsDead = true;

                HandleEntityDeath(entity);
            }
        }
    }

    private void HandleEntityDeath(EntityId entity)
    {
        // Get position for death sound
        Vector3? deathPosition = null;
        if (_world.Components.TryGet(entity, out TransformComponent trans))
        {
            deathPosition = trans.Position;
        }

        // Try to identify what kind of entity this is
        if (_world.Components.Has<EnemyComponent>(entity))
        {
            _world.Events.Publish(new AudioCueEvent(entity, AudioCueType.Death, deathPosition));
            _world.Events.Publish(new EntityDeathEvent(entity));

            // Switch AI to Dead state
            EnemyAIHelpers.ChangeState(_world, entity, EnemyState.Dead);

            // Broadcast death to clients - ADD NULL CHECKS
            if (_world.Components.TryGet(entity, out NetworkObjectComponent netObj))
            {
                if (netObj.NetworkObject != null && netObj.NetworkObject.IsSpawned) // Check if not being destroyed
                {
                    var enemySync = netObj.NetworkObject.GetComponent<EnemyNetworkSyncView>();
                    if (enemySync != null && enemySync.IsSpawned)
                    {
                        enemySync.BroadcastDeathClientRpc();
                    }
                    else
                    {
                        Debug.LogWarning($"EnemyNetworkSyncView not found or not spawned for entity {entity}");
                    }
                }
            }
        }
        else if (_world.Components.Has<PlayerTagComponent>(entity))
        {
            // Handle player death differently
            _world.Events.Publish(new AudioCueEvent(entity, AudioCueType.Death, deathPosition));
            _world.Events.Publish(new EntityDeathEvent(entity));
            Debug.Log("Player has died!");

            // Broadcast player death - ADD NULL CHECKS
            if (_world.Components.TryGet(entity, out NetworkSyncComponent sync))
            {
                if (sync.SyncView != null && sync.SyncView.IsSpawned && sync.SyncView.NetworkObject != null)
                {
                    sync.SyncView.BroadcastDeathClientRpc();
                }
                else
                {
                    Debug.LogWarning($"NetworkSyncView not properly spawned for entity {entity}");
                }
            }
        }
    }

    public void FixedUpdate(float dt) { }

    public void Shutdown() { }
}
