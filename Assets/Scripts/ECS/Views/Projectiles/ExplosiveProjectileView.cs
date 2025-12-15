using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class ExplosiveProjectileView : NetworkBehaviour
{
    private World _world;
    private EntityId _attacker;
    private float _explosionRadius;
    private float _explosionDamage;
    private ParticleSystem _explosionVfx;
    private bool _hasExploded;
    private float _speed;
    private float _lifetime;
    private Vector3 _direction;
    private float _spawnTime;
    private Collider _projectileCollider;

    [SerializeField]
    private LayerMask hitMask;

    public void Initialize(
        World world,
        EntityId attacker,
        float explosionRadius,
        float explosionDamage,
        ParticleSystem explosionVfx,
        float speed,
        float lifetime,
        Vector3 direction
    )
    {
        _world = world;
        _attacker = attacker;
        _explosionRadius = explosionRadius;
        _explosionDamage = explosionDamage;
        _explosionVfx = explosionVfx;
        _hasExploded = false;
        _speed = speed;
        _lifetime = Mathf.Max(0.01f, lifetime);

        // Validate direction so we never move using a stale pooled rotation
        var validatedDirection = direction;
        if (validatedDirection.sqrMagnitude < 0.0001f)
        {
            validatedDirection = transform.forward.sqrMagnitude > 0.0001f ? transform.forward : Vector3.forward;
        }

        validatedDirection.y = 0f;
        _direction = validatedDirection.normalized;
        _spawnTime = Time.time;
        _projectileCollider = GetComponent<Collider>();

        // Enable collider for this component to handle collisions
        if (_projectileCollider != null)
        {
            _projectileCollider.enabled = true;
        }
    }

    private void Update()
    {
        // Only server handles movement and logic
        if (!NetworkManager.Singleton.IsServer)
        {
            return;
        }

        if (_hasExploded)
        {
            return;
        }

        // Move projectile
        transform.position += _direction * _speed * Time.deltaTime;
        transform.rotation = Quaternion.LookRotation(_direction, Vector3.up);

        // Check lifetime
        if (Time.time - _spawnTime >= _lifetime)
        {
            Explode();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_hasExploded)
        {
            return;
        }

        // Server-authoritative collision detection
        if (!NetworkManager.Singleton.IsServer)
        {
            return;
        }

        // Layer detection
        if (!IsInHitMask(other.gameObject))
        {
            return;
        }

        // Trigger explosion on any collision
        Explode();
    }

    private void Explode()
    {
        if (_hasExploded)
        {
            return;
        }

        _hasExploded = true;
        Vector3 explosionPos = transform.position;

        // Disable collider to prevent further collisions
        if (_projectileCollider != null)
        {
            _projectileCollider.enabled = false;
        }

        // Find all entities in explosion radius
        Collider[] hits = Physics.OverlapSphere(explosionPos, _explosionRadius);
        HashSet<EntityId> damagedEntities = new HashSet<EntityId>();

        foreach (Collider hit in hits)
        {
            if (!hit.TryGetComponent(out EntityView targetView))
            {
                continue;
            }

            EntityId targetEntity = targetView.EntityInstance;

            if (targetEntity.Equals(_attacker))
            {
                continue;
            }

            if (damagedEntities.Contains(targetEntity))
            {
                continue;
            }

            damagedEntities.Add(targetEntity);

            // Only damage if has health
            if (_world.Components.Has<HealthDataComponent>(targetEntity))
            {
                _world.Events.Publish(
                    new DamageEvent
                    {
                        Attacker = _attacker,
                        Target = targetEntity,
                        Amount = _explosionDamage,
                    }
                );
            }
        }

        // Spawn explosion VFX
        if (_explosionVfx != null)
        {
            var pool = _world.Services.Resolve<ObjectPoolService>();
            if (pool != null)
            {
                var explosionGo = pool.Get(_explosionVfx.gameObject, explosionPos, Quaternion.identity);
                if (explosionGo.TryGetComponent(out ParticleSystem ps))
                {
                    float duration = ps.main.duration + ps.main.startLifetime.constantMax;
                    Destroy(explosionGo, duration);
                }
            }
            else
            {
                var explosionGo = Instantiate(_explosionVfx.gameObject, explosionPos, Quaternion.identity);
                Destroy(explosionGo, 2f);
            }
        }

        // Play impact sound
        _world.Events.Publish(new AudioCueEvent(_attacker, SoundType.Impact, explosionPos));

        // Broadcast explosion to clients for visual effects
        ExplodeClientRpc(explosionPos);

        // Destroy the projectile
        ReturnOrDestroy();
    }

    [ClientRpc]
    private void ExplodeClientRpc(Vector3 explosionPos)
    {
        // Client-side visual effects only
        if (IsServer)
            return;

        // Spawn explosion VFX on clients
        if (_explosionVfx != null)
        {
            var pool = _world?.Services.Resolve<ObjectPoolService>();
            if (pool != null)
            {
                var explosionGo = pool.Get(_explosionVfx.gameObject, explosionPos, Quaternion.identity);
                if (explosionGo.TryGetComponent(out ParticleSystem ps))
                {
                    float duration = ps.main.duration + ps.main.startLifetime.constantMax;
                    Destroy(explosionGo, duration);
                }
            }
            else
            {
                var explosionGo = Instantiate(_explosionVfx.gameObject, explosionPos, Quaternion.identity);
                Destroy(explosionGo, 2f);
            }
        }
    }

    private void ReturnOrDestroy()
    {
        _hasExploded = false;

        if (!NetworkManager.Singleton.IsServer)
        {
            return;
        }

        NetworkObject netObj = GetComponent<NetworkObject>();
        if (netObj != null && netObj.IsSpawned)
        {
            NetworkObjectDespawner.DespawnNetworkObject(netObj);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private bool IsInHitMask(GameObject obj)
    {
        int objLayer = obj.layer;
        return hitMask == (hitMask | (1 << objLayer));
    }

    private void OnDisable()
    {
        // Reset state when returned to pool
        _hasExploded = false;
        _spawnTime = 0f;
        _direction = Vector3.zero;
    }
}
