using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

public class BuffSpawnerManager : NetworkBehaviour
{
    [Header("Buff Configuration")]
    [SerializeField] private List<BuffSO> _buffsToSpawn;

    // Internal list of valid spawn points found in the scene
    private List<SpawnPoint> _buffSpawnPoints = new List<SpawnPoint>();
    
    // Track occupied spawn points to avoid stacking buffs (optional, but good practice)
    private HashSet<SpawnPoint> _occupiedSpawnPoints = new HashSet<SpawnPoint>();

    public override void OnNetworkSpawn()
    {
        Debug.Log($"[BuffSpawnerManager] OnNetworkSpawn called. IsServer: {IsServer}, IsClient: {IsClient}, IsHost: {IsHost}, NetworkObjectId: {NetworkObjectId}");
        
        if (IsServer)
        {
            FindBuffSpawnPoints();
            SpawnBuffsAtAllPoints();
        }
    }

    private void FindBuffSpawnPoints()
    {
        _buffSpawnPoints.Clear();
        var allSpawnPoints = FindObjectsByType<SpawnPoint>(FindObjectsSortMode.None);
        
        foreach (var sp in allSpawnPoints)
        {
            if (sp.type == SpawnType.Buff)
            {
                _buffSpawnPoints.Add(sp);
            }
        }

        Debug.Log($"[BuffSpawnerManager] Found {_buffSpawnPoints.Count} buff spawn points.");
    }

    private void SpawnBuffsAtAllPoints()
    {
        if (_buffSpawnPoints.Count == 0)
        {
            Debug.LogWarning("[BuffSpawnerManager] No spawn points of type 'Buff' found in scene!");
            return;
        }

        if (_buffsToSpawn.Count == 0)
        {
             Debug.LogWarning("[BuffSpawnerManager] No Buffs defined in _buffsToSpawn list!");
             return;
        }

        // Spawn a random buff at EVERY spawn point
        foreach (var sp in _buffSpawnPoints)
        {
            SpawnRandomBuffAtPoint(sp);
        }
    }

    private void SpawnRandomBuffAtPoint(SpawnPoint sp)
    {
        if (_buffsToSpawn.Count == 0) return;

        BuffSO randomBuff = _buffsToSpawn[Random.Range(0, _buffsToSpawn.Count)];
        SpawnBuff(randomBuff, sp);
    }

    private void SpawnBuff(BuffSO buff, SpawnPoint spawnPoint)
    {
        if (buff == null || buff.Prefab == null)
        {
            Debug.LogError($"[BuffSpawnerManager] Invalid buff data for {buff?.name}");
            return;
        }

        GameObject instance = NetworkObjectSpawner.SpawnNewNetworkObject(buff.Prefab, spawnPoint.transform.position, false);
        
        // Debug: Verify NetworkObject was spawned correctly
        var networkObj = instance.GetComponent<NetworkObject>();
        if (networkObj != null)
        {
            Debug.Log($"[BuffSpawnerManager] Spawned buff '{buff.name}' at {spawnPoint.transform.position}, NetworkObjectId: {networkObj.NetworkObjectId}, IsSpawned: {networkObj.IsSpawned}");
        }
        else
        {
            Debug.LogError($"[BuffSpawnerManager] Buff prefab '{buff.name}' is missing NetworkObject component!");
        }
        
        var pickup = instance.GetComponent<BuffPickupComponent>();
        if (pickup != null)
        {
            pickup.Initialize(buff, spawnPoint, OnBuffPickedUp);
            _occupiedSpawnPoints.Add(spawnPoint);
        }
        else
        {
            Debug.LogError($"[BuffSpawnerManager] Buff prefab missing BuffPickupComponent!");
        }
    }

    private void OnBuffPickedUp(BuffSO buff, SpawnPoint spawnPoint)
    {
        _occupiedSpawnPoints.Remove(spawnPoint);
        StartCoroutine(RespawnRoutine(buff.RespawnTime, spawnPoint));
    }

    private IEnumerator RespawnRoutine(float delay, SpawnPoint spawnPoint)
    {
        yield return new WaitForSeconds(delay);
        SpawnRandomBuffAtPoint(spawnPoint);
    }
}
