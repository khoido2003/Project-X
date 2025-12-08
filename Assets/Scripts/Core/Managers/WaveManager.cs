using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class WaveManager : MonoBehaviour
{
    [Header("Wave Configuration")]
    [Header("Limits")]
    [SerializeField]
    private int maxConcurrentEnemies = 20;

    private int _currentEnemyCount = 0;

    [SerializeField]
    private List<WaveConfiguration> waveConfigs = new();

    [SerializeField]
    private EnemyDefinitionSO bossEnemy;

    [Header("Spawn Points")]
    [SerializeField]
    private Transform[] enemySpawnPoints;

    [SerializeField]
    private Transform[] bossSpawnPoints;

    [Header("Aggressivee spawning")]
    [SerializeField]
    private bool spawnNearPlayers = true;

    [SerializeField]
    private float minDistanceFromPlayer = 10f;

    [SerializeField]
    private float maxDistanceFromPlayer = 20f;

    [SerializeField]
    private bool continousSpawning = true;

    [SerializeField]
    private float continuousSpawnInterval = 5f;

    private World _world;
    private SpawnSystem _spawnSystem;
    private Coroutine _continuousSpawnCoroutine;

    private void Start()
    {
        _world = WorldRunner.Instance.World;

        var field = typeof(WorldRunner).GetField(
            "_spawnSystem",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
        );
        _spawnSystem = field?.GetValue(WorldRunner.Instance) as SpawnSystem;

        if (_spawnSystem == null)
        {
            Debug.LogError("[Wave Manager] Failed to get SpawnSystem");
        }

        // ⭐ FIX: Validate spawn points
        if (enemySpawnPoints == null || enemySpawnPoints.Length == 0)
        {
            Debug.LogError("[WaveManager] No enemy spawn points assigned!");
        }
        else
        {
            Debug.Log($"[WaveManager] Found {enemySpawnPoints.Length} enemy spawn points");
            for (int i = 0; i < enemySpawnPoints.Length; i++)
            {
                if (enemySpawnPoints[i] != null)
                {
                    Debug.Log($"[WaveManager] Spawn point {i}: {enemySpawnPoints[i].position}");
                }
            }
        }
    }

    public void SpawnWave(int round)
    {
        if (!NetworkManager.Singleton.IsServer)
        {
            return;
        }

        WaveConfiguration configuration = GetWaveConfig(round);

        if (configuration == null)
        {
            Debug.LogWarning($"[WaveManager]: No configuration for round {round}");
            return;
        }

        Debug.Log($"[WaveManager] Spawning wave for round {round}: {configuration.enemyCount}");

        if (_continuousSpawnCoroutine != null)
        {
            StopCoroutine(_continuousSpawnCoroutine);
        }

        StartCoroutine(SpawnWaveCoroutine(configuration));

        if (continousSpawning)
        {
            _continuousSpawnCoroutine = StartCoroutine(ContinuousSpawnCoroutine(configuration));
        }
    }

    private IEnumerator SpawnWaveCoroutine(WaveConfiguration config)
    {
        for (int i = 0; i < config.enemyCount; i++)
        {
            Vector3 spawnPos = GetSpawnPosition();

            EnemyDefinitionSO enemyData = config.enemyTypes[UnityEngine.Random.Range(0, config.enemyTypes.Count)];

            SpawnEnemy(enemyData, spawnPos, config);

            yield return new WaitForSeconds(config.spawnInterval);
        }
    }

    private IEnumerator ContinuousSpawnCoroutine(WaveConfiguration config)
    {
        yield return new WaitForSeconds(continuousSpawnInterval);

        while (true)
        {
            if (_currentEnemyCount < maxConcurrentEnemies)
            {
                int spawnCount = UnityEngine.Random.Range(1, 4);

                for (int i = 0; i < spawnCount && _currentEnemyCount < maxConcurrentEnemies; i++)
                {
                    Vector3 spawnPos = GetSpawnPosition();
                    EnemyDefinitionSO enemyData = config.enemyTypes[
                        UnityEngine.Random.Range(0, config.enemyTypes.Count)
                    ];
                    SpawnEnemy(enemyData, spawnPos, config);
                    yield return new WaitForSeconds(0.2f);
                }
            }
            yield return new WaitForSeconds(continuousSpawnInterval);
        }
    }

    private void SpawnEnemy(EnemyDefinitionSO enemyData, Vector3 spawnPos, WaveConfiguration config)
    {
        if (_currentEnemyCount >= maxConcurrentEnemies)
        {
            Debug.LogWarning($"[WaveManager] Max concurrent enemies ({maxConcurrentEnemies}) reached, skipping spawn");
            return;
        }

        EnemyDefinitionSO modifiedEnemy = ScriptableObject.CreateInstance<EnemyDefinitionSO>();

        CopyEnemyData(enemyData, modifiedEnemy);

        modifiedEnemy.maxHealth *= config.healthMultiplier;
        modifiedEnemy.moveSpeed *= config.speedMultiplier;

        if (modifiedEnemy.attacks != null && modifiedEnemy.attacks.Count > 0)
        {
            modifiedEnemy.attacks[0].damage *= config.damageMultiplier;
        }

        modifiedEnemy.detectionRange *= 1.5f;
        modifiedEnemy.checkInterval *= 0.5f;

        _spawnSystem.SpawnNetworkEnemy(modifiedEnemy, spawnPos);
        _currentEnemyCount++;

        _world.Events.Subscribe<EntityDeathEvent>(OnEnemyDeath);
    }

    private void OnEnemyDeath(EntityDeathEvent @event)
    {
        if (_world.Components.Has<EnemyComponent>(@event.Entity))
        {
            _currentEnemyCount = Mathf.Max(0, _currentEnemyCount - 1);
        }
    }

    private Vector3 GetSpawnPosition()
    {
        // Validate spawn points
        if (enemySpawnPoints == null || enemySpawnPoints.Length == 0)
        {
            Debug.LogError("[WaveManager] No spawn points available! Using Vector3.zero");
            return Vector3.zero;
        }

        // If spawn near players is disabled, use random spawn point
        if (!spawnNearPlayers)
        {
            int index = UnityEngine.Random.Range(0, enemySpawnPoints.Length);
            Vector3 pos = enemySpawnPoints[index].position;
            Debug.Log($"[WaveManager] Using spawn point {index}: {pos}");
            return pos;
        }

        // Try to spawn near players
        Vector3 playerPos = FindNearestPlayerPosition();

        // If no valid player position found, use random spawn point
        if (playerPos == Vector3.zero || float.IsNaN(playerPos.x) || float.IsNaN(playerPos.z))
        {
            int fallbackIndex = UnityEngine.Random.Range(0, enemySpawnPoints.Length);
            Vector3 fallbackPos = enemySpawnPoints[fallbackIndex].position;
            Debug.Log($"[WaveManager] No valid player position, using fallback spawn point: {fallbackPos}");
            return fallbackPos;
        }

        // Generate random position around player
        float angle = UnityEngine.Random.Range(0f, 360f) * Mathf.Deg2Rad;
        float distance = UnityEngine.Random.Range(minDistanceFromPlayer, maxDistanceFromPlayer);

        Vector3 offset = new Vector3(Mathf.Cos(angle) * distance, 0f, Mathf.Sin(angle) * distance);
        Vector3 spawnPos = playerPos + offset;

        // Validate with grid system if available
        GridSystem grid = GridSystem.Instance;
        if (grid != null)
        {
            Vector2Int gridPos = grid.GetGridPosition(spawnPos);

            if (grid.IsValidPosition(gridPos))
            {
                gridPos = grid.FindNearestWalkable(gridPos);
                spawnPos = grid.GetWorldPosition(gridPos);
                Debug.Log($"[WaveManager] Spawning near player at grid-validated position: {spawnPos}");
            }
            else
            {
                // Grid position invalid, use closest spawn point
                spawnPos = GetClosestSpawnPoint(playerPos);
                Debug.Log($"[WaveManager] Grid invalid, using closest spawn point: {spawnPos}");
            }
        }
        else
        {
            Debug.LogWarning("[WaveManager] GridSystem not found, using unchecked spawn position");
        }

        return spawnPos;
    }

    private Vector3 GetClosestSpawnPoint(Vector3 referencePos)
    {
        if (enemySpawnPoints == null || enemySpawnPoints.Length == 0)
        {
            return Vector3.zero;
        }

        Vector3 closest = enemySpawnPoints[0].position;
        float closestDist = Vector3.Distance(referencePos, closest);

        for (int i = 1; i < enemySpawnPoints.Length; i++)
        {
            if (enemySpawnPoints[i] == null)
                continue;

            float dist = Vector3.Distance(referencePos, enemySpawnPoints[i].position);
            if (dist < closestDist)
            {
                closestDist = dist;
                closest = enemySpawnPoints[i].position;
            }
        }

        return closest;
    }

    private Vector3 FindNearestPlayerPosition()
    {
        Vector3 nearestPos = Vector3.zero;
        float nearestDist = float.MaxValue;
        bool foundPlayer = false;

        foreach (
            var (entity, player, trans, health) in _world.Components.Query<
                PlayerTagComponent,
                TransformComponent,
                HealthDataComponent
            >()
        )
        {
            if (health.IsDead)
            {
                continue;
            }

            Vector3 playerPos = trans.Position;

            if (!foundPlayer)
            {
                nearestPos = playerPos;
                nearestDist = 0f;
                foundPlayer = true;
            }
        }

        return nearestPos;
    }

    public void SpawnBoss()
    {
        if (!NetworkManager.Singleton.IsServer)
        {
            return;
        }

        // Stop continuous spawning
        if (_continuousSpawnCoroutine != null)
        {
            StopCoroutine(_continuousSpawnCoroutine);
        }

        if (bossEnemy == null)
        {
            Debug.LogError("[WaveManager] No boss enemy configured!");
            return;
        }

        // Find boss spawn point
        Transform bossSpawn = FindBossSpawnPoint();
        Vector3 spawnPos = bossSpawn != null ? bossSpawn.position : Vector3.zero;

        Debug.Log($"[WaveManager] Spawning boss at {spawnPos}");

        _spawnSystem.SpawnNetworkEnemy(bossEnemy, spawnPos);
    }

    private Transform FindBossSpawnPoint()
    {
        int index = UnityEngine.Random.Range(0, bossSpawnPoints.Length - 1);

        return bossSpawnPoints[index];
    }

    public void StopSpawning()
    {
        if (_continuousSpawnCoroutine != null)
        {
            StopCoroutine(_continuousSpawnCoroutine);
            _continuousSpawnCoroutine = null;
        }
    }

    private WaveConfiguration GetWaveConfig(int round)
    {
        if (round - 1 < waveConfigs.Count)
        {
            return waveConfigs[round - 1];
        }

        return new WaveConfiguration
        {
            round = round,
            enemyCount = 5 + (round * 5),
            spawnInterval = Mathf.Max(0.2f, 0.5f - (round * 0.1f)),
            healthMultiplier = 1f + (round * 0.4f),
            damageMultiplier = 1f + (round * 0.3f),
            speedMultiplier = 1f + (round * 0.15f),
            enemyTypes = waveConfigs[0].enemyTypes,
        };
    }

    private void CopyEnemyData(EnemyDefinitionSO source, EnemyDefinitionSO target)
    {
        target.prefab = source.prefab;
        target.enemyName = source.enemyName;
        target.maxHealth = source.maxHealth;
        target.moveSpeed = source.moveSpeed;
        target.attackRange = source.attackRange;
        target.attackCooldown = source.attackCooldown;
        target.damage = source.damage;
        target.isRanged = source.isRanged;
        target.detectionRange = source.detectionRange;
        target.loseTargetRange = source.loseTargetRange;
        target.fieldOfView = source.fieldOfView;
        target.checkInterval = source.checkInterval;
        target.detectionMask = source.detectionMask;
        target.attacks = new List<AttackDefinition>(source.attacks);
    }
}

[Serializable]
public class WaveConfiguration
{
    public int round;
    public int enemyCount;
    public float spawnInterval = 0.5f;
    public float healthMultiplier = 1f;
    public float damageMultiplier = 1f;
    public float speedMultiplier = 1f;
    public List<EnemyDefinitionSO> enemyTypes = new();
}
