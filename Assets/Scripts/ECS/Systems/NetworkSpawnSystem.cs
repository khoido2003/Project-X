using System.Collections.Generic;
using System.Linq;
using Mirror;
using UnityEngine;

/// <summary>
/// Spawns players and enemies on the server using CharacterFactory/EnemyFactory and NetworkServer.Spawn.
/// run only on the server (NetworkServer.active).
/// </summary>
public class NetworkSpawnSystem : ISystem
{
    private readonly SpawnConfigSO _config;
    private readonly List<SpawnPoint> _spawnPoints = new();
    private CharacterFactory _characterFactory;
    private EnemyFactory _enemyFactory;
    private World _world;

    public NetworkSpawnSystem(SpawnConfigSO config)
    {
        _config = config;
    }

    public void Initialize(World world)
    {
        _world = world;
        _characterFactory = new CharacterFactory(world);
        _enemyFactory = new EnemyFactory(world);

        _spawnPoints.Clear();
        _spawnPoints.AddRange(Object.FindObjectsByType<SpawnPoint>(FindObjectsSortMode.None));

        if (!NetworkServer.active)
        {
            Debug.Log("[NetworkSpawnSystem] Not server, skipping spawn.");
            return;
        }

        SpawnPlayersServer();
        SpawnEnemiesServer();
    }

    public void Update(float dt) { }

    public void FixedUpdate(float dt) { }

    public void Shutdown() => _spawnPoints.Clear();

    private void SpawnPlayersServer() { }

    private void SpawnEnemiesServer() { }

    private static void Shuffle<T>(List<T> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int r = Random.Range(i, list.Count);
            (list[i], list[r]) = (list[r], list[i]);
        }
    }
}
