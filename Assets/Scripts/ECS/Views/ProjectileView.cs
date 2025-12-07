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
        _direction = direction.normalized;
        _impactEffect = impactEffect;
        _spawnTime = Time.time;
        _prefabRef = prefabRef;
        _hasHit = false;

        // Always reset position/rotation explicitly before moving
        transform.SetPositionAndRotation(spawnPos, spawnRotation);

        // Reset any internal movement history
        _spawnTime = Time.time;

        // Pool setup
        _pool = world?.Services.Resolve<ObjectPoolService>();
        _usePooling = _pool != null && _prefabRef != null;

        // Enable collider
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

        Debug.DrawRay(transform.position, _direction * 2f, Color.yellow, 0.1f);

        transform.position += _direction * _speed * Time.deltaTime;

        if (Time.time - _spawnTime >= _lifetime)
        {
            ReturnOrDestroy();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_hasHit)
        {
            return;
        }

        if (!NetworkManager.Singleton.IsServer)
        {
            return;
        }

        if (!other.TryGetComponent(out EntityView targetView))
        {
            return;
        }

        if (targetView.EntityInstance.Equals(_attacker))
        {
            return;
        }

        if (!_world.Components.Has<HealthDataComponent>(targetView.EntityInstance))
        {
            return;
        }

        _hasHit = true;

        _world.Events.Publish(
            new DamageEvent
            {
                Attacker = _attacker,
                Target = targetView.EntityInstance,
                Amount = _damage,
            }
        );

        if (_impactEffect != null)
        {
            // spawn impact VFX from pool if possible, else instantiate
            if (_pool != null && _impactEffect.gameObject != null)
            {
                var impactGo = _pool.Get(_impactEffect.gameObject, transform.position, Quaternion.identity);

                if (impactGo.TryGetComponent<ParticleSystem>(out var ps))
                {
                    float duration = ps.main.duration + ps.main.startLifetime.constantMax;
                    Object.Destroy(impactGo, duration);
                }
            }
            else
            {
                GameObject impactGo = Instantiate(_impactEffect.gameObject, transform.position, Quaternion.identity);

                Object.Destroy(impactGo, 2f);
            }
        }

        ReturnOrDestroy();
    }

    private void ReturnOrDestroy()
    {
        if (_hasHit)
        {
            return;
        }

        _hasHit = true;

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

    private void OnDisable()
    {
        // Reset state when returned to pool
        _hasHit = false;
        _spawnTime = 0f;
    }
}
