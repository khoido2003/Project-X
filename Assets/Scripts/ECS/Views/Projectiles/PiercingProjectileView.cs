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

    public void Initialize(World world, EntityId attacker, int maxPierceCount)
    {
        _world = world;
        _attacker = attacker;
        _maxPierceCount = maxPierceCount;
        _currentPierceCount = 0;
        _hitEntities = new HashSet<EntityId>();
        _projectileView = GetComponent<ProjectileView>();
    }

    private void OnTriggerEnter(Collider other)
    {
        // Server-authoritative collision detection
        if (!NetworkManager.Singleton.IsServer)
            return;

        if (!other.TryGetComponent(out EntityView targetView))
        {
            // Hit obstacle - destroy projectile if max pierces reached
            if (_currentPierceCount >= _maxPierceCount)
            {
                Destroy(gameObject);
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
            // Destroy after a tiny delay to ensure damage is processed
            Destroy(gameObject, 0.1f);
        }
    }
}
