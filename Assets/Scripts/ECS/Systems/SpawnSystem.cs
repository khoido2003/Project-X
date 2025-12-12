using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class SpawnSystem : ISystem
{
    private readonly SpawnConfigSO _config;
    private readonly List<SpawnPoint> _spawnPoints = new();
    private CharacterFactory _characterFactory;
    private EnemyFactory _enemyFactory;
    private World _world;

    public SpawnSystem(SpawnConfigSO config)
    {
        _config = config;
    }

    public void Initialize(World world)
    {
        _world = world;
        _characterFactory = new CharacterFactory(world);
        _enemyFactory = new EnemyFactory(world);

        // _spawnPoints.AddRange(Object.FindObjectsByType<SpawnPoint>(FindObjectsSortMode.None));
        //
        // if (_spawnPoints.Count == 0)
        // {
        //     Debug.LogError("No spawn points found in scene!");
        //     return;
        // }
        //
        // SpawnPlayer();
        // SpawnEnemies();
    }

    public void Update(float dt) { }

    public void FixedUpdate(float dt) { }

    public void Shutdown()
    {
        _spawnPoints.Clear();
    }

    ////////////////////////////////////////////////////////////////

    // NETWORK MODE

    public void SpawnNetworkPlayer(ulong clientId, CharacterDefinitionSO characterData, Vector3 spawnPosition)
    {
        if (!NetworkManager.Singleton.IsServer)
        {
            Debug.LogError("Only server can spawn players!");
            return;
        }

        // Validate character data
        if (characterData == null)
        {
            Debug.LogError($"CharacterDefinitionSO is null for client {clientId}");
            return;
        }

        if (characterData.prefab == null)
        {
            Debug.LogError($"Character prefab is null for {characterData.characterName}");
            return;
        }

        // Validate prefab has NetworkObject
        if (characterData.prefab.GetComponent<NetworkObject>() == null)
        {
            Debug.LogError($"Prefab {characterData.prefab.name} doesn't have NetworkObject component!");
            return;
        }

        GameObject playerObj = _characterFactory.CreateNetworkCharacter(
            characterData,
            spawnPosition,
            clientId,
            out EntityId entity
        );

        playerObj.name = $"{characterData.characterName}_Client{clientId}_Entity{entity.Id}";

        // Publish spawn event (for camera to follow if local player)
        _world.Events.Publish(new PlayerSpawnEvent(entity, playerObj, playerObj.transform));
        _world.Events.Publish(new AudioCueEvent(entity, AudioCueType.Spawn));

        Debug.Log($"Spawned player for client {clientId} at {spawnPosition}");
    }

    public void SpawnNetworkEnemy(EnemyDefinitionSO enemyData, Vector3 spawnPosition)
    {
        if (!NetworkManager.Singleton.IsServer)
        {
            Debug.LogError("[SpawnSystem] Only server can spawn enemies");
            return;
        }

        if (enemyData == null)
        {
            Debug.LogError("[SpawnSystem] EnemyDefinitionSO is null");
            return;
        }

        GameObject enemyObj = _enemyFactory.CreateNetworkEnemy(enemyData, spawnPosition, out EntityId entity);

        if (enemyObj != null)
        {
            Debug.Log($"[SpawnSystem] Spawn network enemy {enemyData.enemyName} at {spawnPosition}");
            _world.Events.Publish(new AudioCueEvent(entity, AudioCueType.Spawn));
        }
    }

    public void SpawnEnemyWave(List<EnemyDefinitionSO> enemies, List<Vector3> spawnPositions)
    {
        if (!NetworkManager.Singleton.IsServer)
        {
            return;
        }

        int count = Mathf.Min(enemies.Count, spawnPositions.Count);

        for (int i = 0; i < count; i++)
        {
            SpawnNetworkEnemy(enemies[i], spawnPositions[i]);
        }
    }

    /////////////////////////////////////////////////////////////////////////////

    // TEST MODE

    private void SpawnPlayer()
    {
        if (_spawnPoints.Count == 0)
        {
            _spawnPoints.AddRange(Object.FindObjectsByType<SpawnPoint>(FindObjectsSortMode.None));
        }

        if (_config == null)
        {
            Debug.LogError("SpawnConfigSO is null!");
            return;
        }

        List<SpawnPoint> playerSpawns = _spawnPoints.FindAll(s => s.type == SpawnType.Player);
        if (playerSpawns.Count == 0)
        {
            Debug.LogError("No player spawn points found!");
            return;
        }

        int maxPlayers = Mathf.Min(_config.maxPlayers, playerSpawns.Count);

        int characterCount = Mathf.Min(_config.possiblePlayers.Length, maxPlayers);

        var shuffleSpawns = new List<SpawnPoint>(playerSpawns);
        Shuffle(shuffleSpawns);

        for (int i = 0; i < characterCount; i++)
        {
            CharacterDefinitionSO data = _config.possiblePlayers[i];

            SpawnPoint spawn = shuffleSpawns[i];

            GameObject playerObj = _characterFactory.CreateCharacter(data, spawn.transform.position);

            EntityView view = playerObj.GetComponent<EntityView>();

            // Publish events
            _world.Events.Publish(new PlayerSpawnEvent(view.EntityInstance, playerObj, playerObj.transform));
            _world.Events.Publish(new AudioCueEvent(view.EntityInstance, AudioCueType.Spawn));
        }
    }

    private void SpawnEnemies()
    {
        var enemyPoints = _spawnPoints.FindAll(s => s.type == SpawnType.Enemy);

        if (enemyPoints.Count == 0)
        {
            Debug.LogWarning("No Enemy Spawn points found!");

            return;
        }

        int maxEnemies = Mathf.Min(_config.maxEnemies, enemyPoints.Count);
        int enemyCount = Mathf.Min(_config.possibleEnemies.Length, maxEnemies);

        var shuffleSpawns = new List<SpawnPoint>(enemyPoints);
        Shuffle(shuffleSpawns);

        for (int i = 0; i < enemyCount; i++)
        {
            EnemyDefinitionSO data = _config.possibleEnemies[i];
            SpawnPoint spawn = shuffleSpawns[i];

            GameObject enemyObj = _enemyFactory.CreateEnemy(data, spawn.transform.position, out EntityId entity);
            _world.Events.Publish(new AudioCueEvent(entity, AudioCueType.Spawn));
        }
    }

    /////////////////////////////////////////////////////////////////////////////

    // UTILS

    private void Shuffle(List<SpawnPoint> points)
    {
        for (int i = 0; i < points.Count; i++)
        {
            int rand = Random.Range(i, points.Count);
            (points[i], points[rand]) = (points[rand], points[i]);
        }
    }
}
