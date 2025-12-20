using Unity.Netcode;
using UnityEngine;

public class ProjectileView : NetworkBehaviour
{
    private World _world;
    private EntityId _attacker;
    private float _damage;
    private float _speed;
    private float _lifetime;
    private Vector3 _direction;
    private ParticleSystem _impactEffect;
    private float _spawnTime;
    private GameObject _prefabRef;
    private ObjectPoolService _pool;
    private bool _usePooling;
    private bool _hasHit;

    [SerializeField]
    private LayerMask hitMask;

    [Header("Impact VFX (for client sync)")]
    [SerializeField]
    private ParticleSystem impactEffectPrefab;

    public void Initialize(
        World world,
        EntityId attacker,
        float damage,
        float speed,
        float lifetime,
        Vector3 direction,
        ParticleSystem impactEffect,
        GameObject prefabRef,
        Vector3 spawnPos,
        Quaternion spawnRotation
    )
    {
        _world = world;
        _attacker = attacker;
        _damage = damage;
        _speed = speed;
        _lifetime = Mathf.Max(0.01f, lifetime);
        _spawnTime = Time.time;
        _prefabRef = prefabRef;
        _hasHit = false;

        // Always reset position/rotation explicitly before moving
        transform.SetPositionAndRotation(spawnPos, spawnRotation);

        // Derive a reliable direction. If caller passed an invalid vector (e.g. zero from a bad input),
        // fall back to the provided spawn rotation instead of the object's old forward (which can be stale when pooled).
        var validatedDirection = direction;
        if (validatedDirection.sqrMagnitude < 0.0001f)
        {
            validatedDirection = spawnRotation * Vector3.forward;
        }
        if (validatedDirection.sqrMagnitude < 0.0001f)
        {
            validatedDirection = Vector3.forward;
        }

        // Keep horizontal aiming (mouse world usually lies on ground plane)
        validatedDirection.y = 0f;
        _direction = validatedDirection.normalized;

        _impactEffect = impactEffect;

        // Set rotation based on direction
        // Force horizontal rotation - only rotate around Y axis
        if (_direction.sqrMagnitude > 0.0001f)
        {
            // Create a purely horizontal rotation by only using the Y euler angle
            Quaternion lookRot = Quaternion.LookRotation(_direction, Vector3.up);
            Vector3 euler = lookRot.eulerAngles;
            euler.x = 0f; // No pitch
            euler.z = 0f; // No roll
            transform.rotation = Quaternion.Euler(euler);
        }
        else
        {
            transform.rotation = Quaternion.Euler(0f, spawnRotation.eulerAngles.y, 0f);
        }

        // Reset any internal movement history
        _spawnTime = Time.time;

        // Pool setup - NetworkObjects CANNOT be pooled (reparenting causes SpawnStateException)
        bool isNetworkObject = TryGetComponent(out Unity.Netcode.NetworkObject netObj);
        _pool = world?.Services.Resolve<ObjectPoolService>();
        _usePooling = _pool != null && _prefabRef != null && !isNetworkObject;

        var col = GetComponent<Collider>();
        if (col != null)
        {
            col.enabled = true;
        }

        // For NetworkObjects, sync movement data to clients
        if (isNetworkObject && netObj.IsSpawned && NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            SyncMovementClientRpc(_speed, _direction, _lifetime, _attacker.Id);
        }
    }

    [ClientRpc]
    private void SyncMovementClientRpc(float speed, Vector3 direction, float lifetime, int attackerId)
    {
        // Server already has these values from Initialize()
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer) return;

        _speed = speed;
        _direction = direction;
        _lifetime = lifetime;
        _spawnTime = Time.time;
        _hasHit = false;
        _attacker = new EntityId(attackerId);

        // Use serialized prefab as fallback for impact effect
        if (_impactEffect == null && impactEffectPrefab != null)
        {
            _impactEffect = impactEffectPrefab;
        }

        // Set rotation to match direction
        if (_direction.sqrMagnitude > 0.0001f)
        {
            Quaternion lookRot = Quaternion.LookRotation(_direction, Vector3.up);
            Vector3 euler = lookRot.eulerAngles;
            euler.x = 0f;
            euler.z = 0f;
            transform.rotation = Quaternion.Euler(euler);
        }
    }

    private void Update()
    {
        if (_hasHit)
        {
            return;
        }

        // Ensure direction is normalized
        if (_direction.sqrMagnitude < 0.9f)
        {
            _direction = _direction.normalized;
        }

        // Move projectile in the direction
        Vector3 movement = _direction * _speed * Time.deltaTime;
        transform.position += movement;

        // NOTE: Rotation is set once in Initialize() and NOT updated here to prevent jitter
        // If projectile needs to change direction during flight (e.g., homing), use a separate method

        Debug.DrawRay(transform.position, _direction * 2f, Color.yellow, 0.1f);

        if (Time.time - _spawnTime >= _lifetime)
        {
            ReturnOrDestroy();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_hasHit)
        {
            return;
        }

        // Spawn protection - ignore collisions briefly after spawn
        // This prevents projectile from hitting the shooter's gun/weapon parts
        float timeSinceSpawn = Time.time - _spawnTime;
        if (timeSinceSpawn < 0.05f)
        {
            return;
        }

        // layer detection (obstacle, player, enemy, etc)
        if (!IsInHitMask(other.gameObject))
        {
            return;
        }

        if (!other.TryGetComponent(out EntityView targetView))
        {
            HitAndReturn();
            return;
        }

        if (targetView.EntityInstance.Equals(_attacker))
        {
            return;
        }

        // Only SERVER handles damage - clients just show visual effects
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            if (_world != null && _world.Components.Has<HealthDataComponent>(targetView.EntityInstance))
            {
                _world.Events.Publish(
                    new DamageEvent
                    {
                        Attacker = _attacker,
                        Target = targetView.EntityInstance,
                        Amount = _damage,
                    }
                );
            }

            Vector3 hitPos = other.ClosestPoint(transform.position);

            // Play impact sound at hit position
            if (_world != null)
            {
                _world.Events.Publish(new AudioCueEvent(_attacker, SoundType.Impact, hitPos));
            }
        }

        // BOTH server and client show visual impact effect
        HitAndReturn();
    }

    private void HitAndReturn()
    {
        if (_hasHit)
            return;
        _hasHit = true;

        // spawn effect
        if (_impactEffect != null)
        {
            var impactGo = Instantiate(_impactEffect.gameObject, transform.position, Quaternion.identity);
            Destroy(impactGo, 2f);
        }

        ReturnOrDestroy();
    }

    private void ReturnOrDestroy()
    {
        _hasHit = false;

        // Check if this is a NetworkObject - those can't be pooled normally
        if (TryGetComponent(out Unity.Netcode.NetworkObject netObj) && netObj.IsSpawned)
        {
            // Despawn network object - this will destroy it on all clients
            if (Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsServer)
            {
                netObj.Despawn(true);
            }
            return;
        }

        if (_usePooling && _prefabRef != null)
        {
            _pool.Return(_prefabRef, gameObject);
        }
        else
        {
            // no pool -> destroy to avoid stale inactive object reuse
            Destroy(gameObject);
        }
    }

    private bool IsInHitMask(GameObject obj)
    {
        // If hitMask is not set (0), accept all collisions
        // This allows client visual-only projectiles to work without configured hitMask
        if (hitMask == 0)
        {
            return true;
        }

        int objLayer = obj.layer;

        // Convert each included layer of hitMask into readable names
        return hitMask == (hitMask | (1 << objLayer));
    }

    private void OnDisable()
    {
        // Reset state when returned to pool
        _hasHit = false;
        _spawnTime = 0f;
        _direction = Vector3.zero;
    }
}
