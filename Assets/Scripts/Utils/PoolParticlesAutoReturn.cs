using UnityEngine;

public class PooledParticlesAutoReturn : MonoBehaviour
{
    private ObjectPoolService _pool;
    private GameObject _prefab;
    private ParticleSystem _ps;

    public void Init(ObjectPoolService pool, GameObject prefab)
    {
        _pool = pool;
        _prefab = prefab;
        _ps = GetComponent<ParticleSystem>();
    }

    private void OnEnable()
    {
        if (_ps != null)
        {
            StartCoroutine(ReturnAfter(_ps.main.duration));
        }
    }

    private System.Collections.IEnumerator ReturnAfter(float delay)
    {
        yield return new WaitForSeconds(delay);
        _pool.Return(_prefab, gameObject);
    }
}
