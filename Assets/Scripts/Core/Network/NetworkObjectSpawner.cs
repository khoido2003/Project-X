using Unity.Netcode;
using UnityEngine;

public class NetworkObjectSpawner
{
    public static GameObject SpawnNewNetworkObject(GameObject prefab, bool destroyedWithScene = true)
    {
#if UNITY_EDITOR
        if (!NetworkManager.Singleton.IsServer)
        {
            Debug.LogError("ERROR: Spawninng not happening in the server");
        }
#endif

        GameObject newGameObject = Object.Instantiate(prefab, Vector3.zero, Quaternion.identity);

        NetworkObject newGameOjectNetworkObject = newGameObject.GetComponent<NetworkObject>();

        newGameOjectNetworkObject.Spawn(destroyedWithScene);

        return newGameObject;
    }

    public static GameObject SpawnNewNetworkObject(GameObject prefab, Vector3 position, bool destroyedWithScene = true)
    {
#if UNITY_EDITOR
        if (!NetworkManager.Singleton.IsServer)
        {
            Debug.LogError("ERROR: Spawninng not happening in the server");
        }
#endif

        GameObject newGameObject = Object.Instantiate(prefab, position, Quaternion.identity);

        NetworkObject newGameOjectNetworkObject = newGameObject.GetComponent<NetworkObject>();

        newGameOjectNetworkObject.Spawn(destroyedWithScene);

        return newGameObject;
    }

    public static GameObject SpawnNewNetworkObject(
        GameObject prefab,
        Vector3 position,
        Quaternion rotation,
        bool destroyedWithScene = true
    )
    {
#if UNITY_EDITOR
        if (!NetworkManager.Singleton.IsServer)
        {
            Debug.LogError("ERROR: Spawninng not happening in the server");
        }
#endif

        GameObject newGameObject = Object.Instantiate(prefab, position, rotation);

        NetworkObject newGameOjectNetworkObject = newGameObject.GetComponent<NetworkObject>();

        newGameOjectNetworkObject.Spawn(destroyedWithScene);

        return newGameObject;
    }

    public static GameObject SpawnNewNetworkObjectAsPlayerObject(
        GameObject prefab,
        Vector3 position,
        ulong newCLientOwnerId,
        bool destroyWithScene = true
    )
    {
#if UNITY_EDITOR
        if (!NetworkManager.Singleton.IsServer)
        {
            Debug.LogError("ERROR: Spawninng not happening in the server");
        }
#endif

        GameObject newGameObject = Object.Instantiate(prefab, position, Quaternion.identity);

        NetworkObject newGameOjectNetworkObject = newGameObject.GetComponent<NetworkObject>();

        newGameOjectNetworkObject.SpawnAsPlayerObject(newCLientOwnerId, destroyWithScene);

        return newGameObject;
    }

    public static GameObject SpawnNewNetworkObjectChangeOwnershipToClient(
        GameObject prefab,
        Vector3 position,
        ulong newClientOwnerId,
        bool destroyWithScene = true
    )
    {
#if UNITY_EDITOR
        if (!NetworkManager.Singleton.IsServer)
        {
            Debug.LogError("ERROR: Spawning not happening in the server!");
        }
#endif
        // We're first instantiating the new instance in the host client
        GameObject newGameObject = Object.Instantiate(prefab, position, Quaternion.identity);

        // Replicating that same new instance to all connected clients
        NetworkObject newGameObjectNetworkObject = newGameObject.GetComponent<NetworkObject>();
        newGameObjectNetworkObject.SpawnWithOwnership(newClientOwnerId, destroyWithScene);

        return newGameObject;
    }
}
