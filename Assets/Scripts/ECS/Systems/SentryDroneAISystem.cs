using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Server-only system that handles Explosive Drone AI behavior.
/// Drones fly toward their target and explode on contact or timeout.
/// </summary>
public class SentryDroneAISystem : ISystem
{
    private World _world;
    private EntityViewRegistry _registry;
    private List<EntityId> _dronesToRemove = new();

    // Distance at which drone detonates when reaching target
    private const float DETONATION_DISTANCE = 1.5f;
    
    // Position sync interval to reduce network traffic
    private const float POSITION_SYNC_INTERVAL = 0.1f; // 10 syncs per second
    private Dictionary<EntityId, float> _lastSyncTime = new();

    public void Initialize(World world)
    {
        _world = world;
        _registry = world.Services.Resolve<EntityViewRegistry>();
    }

    public void Update(float dt)
    {
        // Server-only system
        if (!NetworkManager.Singleton.IsServer)
        {
            return;
        }

        _dronesToRemove.Clear();

        foreach (var (entity, drone) in _world.Components.Query<SentryDroneComponent>())
        {
            // Skip already exploded drones
            if (drone.HasExploded)
            {
                continue;
            }

            // Get drone view
            EntityView droneView = drone.DroneView;
            if (droneView == null)
            {
                Debug.LogError($"[SentryDroneAI] Drone {entity.Id} has NULL DroneView! Removing.");
                _dronesToRemove.Add(entity);
                continue;
            }

            // Check timeout - auto-detonate
            float timeRemaining = drone.DetonationTime - Time.time;
            if (timeRemaining <= 0)
            {
                Explode(entity, drone, droneView.transform.position);
                _dronesToRemove.Add(entity);
                continue;
            }
            // Try to find or update target
            bool hasValidTarget = false;
            
            // Check if current target is still valid
            if (!drone.TargetEntity.Equals(default))
            {
                if (_registry.TryGet(drone.TargetEntity, out EntityView targetView))
                {
                    if (_world.Components.TryGet(drone.TargetEntity, out HealthDataComponent targetHealth) && !targetHealth.IsDead)
                    {
                        drone.TargetPosition = targetView.transform.position;
                        hasValidTarget = true;
                    }
                }
            }
            
            // If no valid target, scan for new enemy
            if (!hasValidTarget)
            {
                EntityId newTarget = FindNearestEnemy(droneView.transform.position, 20f); // Extended detection range
                if (!newTarget.Equals(default) && _registry.TryGet(newTarget, out EntityView newTargetView))
                {
                    drone.TargetEntity = newTarget;
                    drone.TargetPosition = newTargetView.transform.position;
                    hasValidTarget = true;
                }
            }

            Vector3 currentPos = droneView.transform.position;
            Vector3 moveTarget;
            
            if (hasValidTarget)
            {
                // Fly toward enemy
                moveTarget = drone.TargetPosition;
                
                float distanceToTarget = Vector3.Distance(currentPos, moveTarget);
                
                // Check if reached target - EXPLODE!
                if (distanceToTarget <= DETONATION_DISTANCE)
                {
                    Explode(entity, drone, currentPos);
                    _dronesToRemove.Add(entity);
                    continue;
                }
            }
            else
            {
                // No enemy - follow owner (hover near them)
                if (_registry.TryGet(drone.Owner, out EntityView ownerView))
                {
                    // Position slightly ahead and above owner
                    Vector3 ownerPos = ownerView.transform.position;
                    Vector3 ownerForward = ownerView.transform.forward;
                    moveTarget = ownerPos + ownerForward * 2f + Vector3.up * 2f;
                }
                else
                {
                    // Can't find owner - just hover in place with slight bobbing motion
                    Debug.LogWarning($"[SentryDroneAI] Drone {entity.Id} can't find owner {drone.Owner.Id}");
                    moveTarget = currentPos + Vector3.up * Mathf.Sin(Time.time * 2f) * 0.1f;
                }
            }

            // Calculate movement
            Vector3 direction = (moveTarget - currentPos);
            direction.y = Mathf.Clamp(direction.y, -0.5f, 0.5f); // Limit vertical movement
            
            // Move toward target
            Vector3 moveDir = direction.normalized;
            float moveSpeed = hasValidTarget ? drone.FlightSpeed : drone.FlightSpeed * 0.7f; // Slower when following owner
            Vector3 newPos = currentPos + moveDir * moveSpeed * dt;
            droneView.transform.position = newPos;

            // Face movement direction
            if (moveDir.sqrMagnitude > 0.01f)
            {
                droneView.transform.rotation = Quaternion.Slerp(
                    droneView.transform.rotation,
                    Quaternion.LookRotation(moveDir),
                    dt * 10f
                );
            }

            // Update TransformComponent
            if (_world.Components.TryGet(entity, out TransformComponent trans))
            {
                trans.Position = newPos;
                trans.Rotation = droneView.transform.rotation;
            }
            
            // Periodically sync position to clients for visual drone tracking
            if (!_lastSyncTime.TryGetValue(entity, out float lastSync) || Time.time - lastSync >= POSITION_SYNC_INTERVAL)
            {
                _lastSyncTime[entity] = Time.time;
                
                // Get the owner's NetworkSyncView to send RPC
                if (_world.Components.TryGet(drone.Owner, out NetworkSyncComponent ownerSync) && ownerSync.SyncView != null)
                {
                    ownerSync.SyncView.BroadcastDronePositionClientRpc(newPos, droneView.transform.rotation);
                }
            }
        }

        // Cleanup exploded drones
        foreach (var droneId in _dronesToRemove)
        {
            // Clean up sync timer
            _lastSyncTime.Remove(droneId);
            
            // Destroy drone entity and view
            if (_world.Components.TryGet(droneId, out SentryDroneComponent drone) && drone.DroneView != null)
            {
                Object.Destroy(drone.DroneView.gameObject);
            }
            _world.DestroyEntity(droneId);
        }
    }

    public void FixedUpdate(float dt) { }

    /// <summary>
    /// Find the nearest enemy within range
    /// </summary>
    private EntityId FindNearestEnemy(Vector3 position, float range)
    {
        EntityId nearestEnemy = default;
        float nearestDistance = float.MaxValue;

        foreach (var (entity, _) in _world.Components.Query<EnemyComponent>())
        {
            // Skip dead enemies
            if (_world.Components.TryGet(entity, out HealthDataComponent health) && health.IsDead)
            {
                continue;
            }

            if (!_registry.TryGet(entity, out EntityView enemyView))
            {
                continue;
            }

            float distance = Vector3.Distance(position, enemyView.transform.position);
            if (distance <= range && distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestEnemy = entity;
            }
        }

        return nearestEnemy;
    }

    /// <summary>
    /// Explode the drone, dealing AoE damage to all enemies in radius
    /// </summary>
    private void Explode(EntityId droneEntity, SentryDroneComponent drone, Vector3 position)
    {
        if (drone.HasExploded)
        {
            return;
        }

        drone.HasExploded = true;

        // Play explosion VFX
        if (drone.SkillData != null && drone.SkillData.explosionVfxPrefab != null)
        {
            var vfx = Object.Instantiate(drone.SkillData.explosionVfxPrefab, position, Quaternion.identity);
            vfx.Play();
            Object.Destroy(vfx.gameObject, 3f);
        }

        // Play explosion sound
        if (drone.SkillData != null && drone.SkillData.explosionSound != null)
        {
            AudioHelper.PlaySound3D(_world, drone.SkillData.explosionSound, AudioCategory.Player, position);
        }
        
        // Broadcast explosion position to clients via owner's NetworkSyncView
        if (_world.Components.TryGet(drone.Owner, out NetworkSyncComponent ownerSync) && ownerSync.SyncView != null)
        {
            ownerSync.SyncView.BroadcastDroneExplosionClientRpc(position);
        }

        // Deal AoE damage to all enemies in radius
        foreach (var (entity, _) in _world.Components.Query<EnemyComponent>())
        {
            if (!_registry.TryGet(entity, out EntityView enemyView))
            {
                continue;
            }

            // Skip dead enemies
            if (_world.Components.TryGet(entity, out HealthDataComponent health) && health.IsDead)
            {
                continue;
            }

            float distance = Vector3.Distance(position, enemyView.transform.position);
            if (distance <= drone.ExplosionRadius)
            {
                // Calculate damage falloff (full damage at center, less at edges)
                float damageMultiplier = 1f - (distance / drone.ExplosionRadius) * 0.5f;
                float damage = drone.ExplosionDamage * damageMultiplier;

                _world.Events.Publish(new DamageEvent
                {
                    Target = entity,
                    Attacker = drone.Owner, // Credit to player
                    Amount = damage
                });
            }
        }

        // Also damage other players in PvPvE
        foreach (var (entity, _) in _world.Components.Query<PlayerTagComponent>())
        {
            // Don't damage the owner
            if (entity.Equals(drone.Owner))
            {
                continue;
            }

            if (!_registry.TryGet(entity, out EntityView playerView))
            {
                continue;
            }

            // Skip dead players
            if (_world.Components.TryGet(entity, out HealthDataComponent health) && health.IsDead)
            {
                continue;
            }

            float distance = Vector3.Distance(position, playerView.transform.position);
            if (distance <= drone.ExplosionRadius)
            {
                float damageMultiplier = 1f - (distance / drone.ExplosionRadius) * 0.5f;
                float damage = drone.ExplosionDamage * damageMultiplier;

                _world.Events.Publish(new DamageEvent
                {
                    Target = entity,
                    Attacker = drone.Owner,
                    Amount = damage
                });
            }
        }
    }

    private void DespawnDrone(EntityId droneEntity)
    {
        if (!_world.Components.TryGet(droneEntity, out SentryDroneComponent drone))
        {
            return;
        }

        // Destroy the drone's NetworkObject
        if (_world.Components.TryGet(droneEntity, out NetworkObjectComponent netObjComp))
        {
            if (netObjComp.NetworkObject != null && netObjComp.NetworkObject.IsSpawned)
            {
                netObjComp.NetworkObject.Despawn(true);
            }
        }

        // Clean up entity
        _world.DestroyEntity(droneEntity);
    }

    public void Shutdown()
    {
        _dronesToRemove.Clear();
    }
}
