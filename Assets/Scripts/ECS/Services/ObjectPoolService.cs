using System.Collections.Generic;
using UnityEngine;

public class ObjectPoolService : IObjectPoolService
{
    private readonly Dictionary<GameObject, Queue<GameObject>> _pools = new();
    private readonly Transform _rootParent;

    public ObjectPoolService(string rootName = "ObjectPool_Root")
    {
        var rootGO = GameObject.Find(rootName) ?? new GameObject(rootName);
        _rootParent = rootGO.transform;
    }

    public GameObject Get(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null)
        {
            Debug.LogError("[ObjectPoolService] Tried to spawn null prefab.");
            return null;
        }

        if (!_pools.TryGetValue(prefab, out var pool))
        {
            pool = new Queue<GameObject>();
            _pools[prefab] = pool;
        }

        GameObject instance;
        if (pool.Count > 0)
        {
            instance = pool.Dequeue();

            instance.transform.SetPositionAndRotation(position, rotation);

            instance.SetActive(true);
        }
        else
        {
            instance = Object.Instantiate(prefab, position, rotation, _rootParent);
        }

        return instance;
    }

    public void Return(GameObject prefab, GameObject instance)
    {
        if (prefab == null || instance == null)
        {
            return;
        }

        instance.SetActive(false);

        if (!_pools.ContainsKey(prefab))
        {
            _pools[prefab] = new Queue<GameObject>();
        }

        _pools[prefab].Enqueue(instance);
    }

    public void ClearAll()
    {
        foreach (var pair in _pools)
        {
            foreach (var obj in pair.Value)
            {
                if (obj != null)
                    Object.Destroy(obj);
            }
        }

        _pools.Clear();
    }
}
