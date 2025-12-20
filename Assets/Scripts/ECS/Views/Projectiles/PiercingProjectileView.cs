using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class PiercingProjectileView : NetworkBehaviour
{
    private World _world;
    private EntityId _attacker;
    private int _maxPierceCount;
    private int _currentPierceCount;
    private HashSet<EntityId> _hitEntities;
    private ProjectileView _projectileView;
    private float _spawnTime;

    [SerializeField]
    private LayerMask hitMask;

    public void Initialize(World world, EntityId attacker, int maxPierceCount)
    {
        _world = world;
        _attacker = attacker;
        _maxPierceCount = maxPierceCount;
        _currentPierceCount = 0;
        _hitEntities = new HashSet<EntityId>();
        _projectileView = GetComponent<ProjectileView>();
        _spawnTime = Time.time;
    }

    private void OnTriggerEnter(Collider other)
    {
        // Server-authoritative collision detection
        if (!NetworkManager.Singleton.IsServer)
            return;

        // Spawn protection - ignore collisions briefly after spawn
        // This prevents projectile from hitting the shooter's gun/weapon parts
        float timeSinceSpawn = Time.time - _spawnTime;
        if (timeSinceSpawn < 0.05f)
            return;

        // Check hitMask layer filtering
        if (hitMask != 0 && !IsInHitMask(other.gameObject))
        {
            return;
        }

        if (!other.TryGetComponent(out EntityView targetView))
        {
            // Hit obstacle - destroy projectile if max pierces reached
            if (_currentPierceCount >= _maxPierceCount)
            {
                DespawnOrDestroy();
            }
            return;
        }

        EntityId targetEntity = targetView.EntityInstance;

        if (targetEntity.Equals(_attacker))
        {
            return;
        }

        if (_hitEntities.Contains(targetEntity))
        {
            return;
        }

        // Track this entity as hit
        _hitEntities.Add(targetEntity);
        _currentPierceCount++;

        // If we've pierced max targets, destroy the projectile after a small delay
        // to allow ProjectileView to process the damage
        if (_currentPierceCount >= _maxPierceCount)
        {
            // Disable collider to prevent further hits
            var col = GetComponent<Collider>();
            if (col != null)
            {
                col.enabled = false;
            }
            // Despawn after a tiny delay to ensure damage is processed
            Invoke(nameof(DespawnOrDestroy), 0.1f);
        }
    }

    private bool IsInHitMask(GameObject obj)
    {
        int objLayer = obj.layer;
        return hitMask == (hitMask | (1 << objLayer));
    }

    private void DespawnOrDestroy()
    {
        // Check if this is a NetworkObject - properly despawn instead of just destroying
        if (TryGetComponent(out NetworkObject netObj) && netObj.IsSpawned)
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            {
                netObj.Despawn(true);
            }
            return;
        }

        Destroy(gameObject);
    }
}
