using System.Collections.Generic;
using UnityEngine;

public class NetworkEntityRegistry : MonoBehaviour
{
    public static NetworkEntityRegistry Instance { get; private set; }

    private readonly Dictionary<int, EntityId> connectionToEntity = new();
    private readonly Dictionary<int, EntityId> netIdToEntity = new();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void Register(int connectionId, EntityId entity)
    {
        connectionToEntity[connectionId] = entity;
    }

    public void Unregister(int connectionId)
    {
        if (connectionToEntity.TryGetValue(connectionId, out var e))
        {
            connectionToEntity.Remove(connectionId);
        }
    }

    public bool TryGetEntityForConnection(int connectionId, out EntityId entity)
    {
        return connectionToEntity.TryGetValue(connectionId, out entity);
    }
}
