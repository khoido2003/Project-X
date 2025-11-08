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

    private void SpawnPlayersServer()
    {
        if (_config == null)
        {
            Debug.LogError("NetworkSpawnSystem: SpawnConfigSO is null!");
            return;
        }

        var playerSpawns = _spawnPoints.Where(s => s.type == SpawnType.Player).ToList();
        if (playerSpawns.Count == 0)
        {
            Debug.LogWarning("NetworkSpawnSystem: no player spawn points");
            return;
        }

        // Prefer authoritative choices from GameSession if present
        var session = GameSession.Instance;
        if (session != null && session.playerChoices.Count > 0)
        {
            var spawnList = new List<SpawnPoint>(playerSpawns);
            Shuffle(spawnList);
            int spawnIndex = 0;

            foreach (var kvp in session.playerChoices)
            {
                int playerId = kvp.Key;
                var choice = kvp.Value;
                var character = choice.GetCharacter();
                if (character == null)
                    continue;

                var spawn = spawnList[spawnIndex % spawnList.Count];
                spawnIndex++;

                var playerObj = _characterFactory.CreateCharacter(character, spawn.transform.position);

                // Find the connection by id and assign ownership
                var conn = NetworkServer.connections.Values.FirstOrDefault(c => c.connectionId == playerId);
                if (conn != null)
                {
                    NetworkServer.Spawn(playerObj, conn);
                }
                else
                {
                    NetworkServer.Spawn(playerObj);
                }

                var view = playerObj.GetComponent<EntityView>();
                _world.Events.Publish(new PlayerSpawnEvent(view.EntityInstance, playerObj, playerObj.transform));
            }
        }
        else
        {
            // Fallback: spawn generic players for each connection
            var spawnList = new List<SpawnPoint>(playerSpawns);
            Shuffle(spawnList);
            int idx = 0;

            foreach (var conn in NetworkServer.connections.Values)
            {
                // pick a character from config (round-robin)
                var ch = _config.possiblePlayers[idx % _config.possiblePlayers.Length];
                var spawn = spawnList[idx % spawnList.Count];

                var playerObj = _characterFactory.CreateCharacter(ch, spawn.transform.position);
                NetworkServer.Spawn(playerObj, conn);

                var view = playerObj.GetComponent<EntityView>();
                _world.Events.Publish(new PlayerSpawnEvent(view.EntityInstance, playerObj, playerObj.transform));
                idx++;
            }
        }
    }

    private void SpawnEnemiesServer()
    {
        var enemyPoints = _spawnPoints.Where(s => s.type == SpawnType.Enemy).ToList();
        if (enemyPoints.Count == 0)
            return;

        int enemyCount = Mathf.Min(_config.maxEnemies, enemyPoints.Count);
        for (int i = 0; i < enemyCount; i++)
        {
            EnemyDefinitionSO data = _config.possibleEnemies[i % _config.possibleEnemies.Length];
            SpawnPoint spawn = enemyPoints[i % enemyPoints.Count];

            var enemyObj = _enemyFactory.CreateEnemy(data, spawn.transform.position);
            NetworkServer.Spawn(enemyObj);
        }
    }

    private static void Shuffle<T>(List<T> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int r = Random.Range(i, list.Count);
            (list[i], list[r]) = (list[r], list[i]);
        }
    }
}
