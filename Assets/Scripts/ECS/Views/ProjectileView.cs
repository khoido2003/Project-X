using UnityEngine;

public class ProjectileView : MonoBehaviour
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

        // Always reset position/rotation explicitly before moving
        transform.SetPositionAndRotation(spawnPos, spawnRotation);

        // Reset any internal movement history
        _spawnTime = Time.time;

        try
        {
            _pool = _world.Services.Resolve<ObjectPoolService>();
        }
        catch
        {
            _pool = null;
        }
        _usePooling = _pool != null && _prefabRef != null;
    }

    private void Update()
    {
        Debug.DrawRay(transform.position, _direction * 2f, Color.yellow, 0.1f);
        transform.position += _direction * _speed * Time.deltaTime;

        if (Time.time - _spawnTime >= _lifetime)
        {
            ReturnOrDestroy();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.TryGetComponent(out EntityView targetView))
        {
            return;
        }

        if (!_world.Components.TryGet(_attacker, out PlayerTagComponent _))
        {
            return;
        }

        if (targetView.EntityInstance.Equals(_attacker))
        {
            return;
        }

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
            }
            else
            {
                Instantiate(_impactEffect, transform.position, Quaternion.identity);
            }
        }

        ReturnOrDestroy();
    }

    private void ReturnOrDestroy()
    {
        if (_usePooling)
        {
            _pool.Return(_prefabRef, gameObject);
        }
        else
        {
            // no pool -> destroy to avoid stale inactive object reuse
            Destroy(gameObject);
        }
    }
}
