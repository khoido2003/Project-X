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

        // CRITICAL: Set rotation based on direction, not spawnRotation
        if (_direction.sqrMagnitude > 0.0001f)
        {
            transform.rotation = Quaternion.LookRotation(_direction, Vector3.up);
        }
        else
        {
            transform.rotation = spawnRotation;
        }

        // Reset any internal movement history
        _spawnTime = Time.time;

        // Pool setup
        _pool = world?.Services.Resolve<ObjectPoolService>();
        _usePooling = _pool != null && _prefabRef != null;

        var col = GetComponent<Collider>();
        if (col != null)
        {
            col.enabled = true;
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

        // CRITICAL FIX: Spawn protection - ignore collisions briefly after spawn
        // This prevents projectile from hitting the shooter's gun/weapon parts
        float timeSinceSpawn = Time.time - _spawnTime;
        if (timeSinceSpawn < 0.05f)
        {
            return;
        }

        if (!NetworkManager.Singleton.IsServer)
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
            // It's an obstacle or non-entity layer, just explode and return
            HitAndReturn();
            return;
        }

        // Don't hit the shooter
        if (targetView.EntityInstance.Equals(_attacker))
            return;

        // Only damage if has health
        if (_world.Components.Has<HealthDataComponent>(targetView.EntityInstance))
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
        _world.Events.Publish(new AudioCueEvent(_attacker, SoundType.Impact, hitPos));

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
