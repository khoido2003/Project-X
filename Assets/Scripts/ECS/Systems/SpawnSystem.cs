using System.Collections.Generic;
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

        _spawnPoints.AddRange(Object.FindObjectsByType<SpawnPoint>(FindObjectsSortMode.None));

        if (_spawnPoints.Count == 0)
        {
            Debug.LogError("No spawn points found in scene!");
            return;
        }

        SpawnPlayer();
        SpawnEnemies();
    }

    public void Update(float dt) { }

    public void FixedUpdate(float dt) { }

    public void Shutdown()
    {
        _spawnPoints.Clear();
    }

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

            GameObject enemyObj = _enemyFactory.CreateEnemy(data, spawn.transform.position);
        }
    }

    private void Shuffle(List<SpawnPoint> points)
    {
        for (int i = 0; i < points.Count; i++)
        {
            int rand = Random.Range(i, points.Count);
            (points[i], points[rand]) = (points[rand], points[i]);
        }
    }
}
