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
    private int maxConcurrentEnemies = 12;

    private bool _deathEventSubscribed = false;

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

        // Subscribe to death events once
        if (!_deathEventSubscribed && _world != null)
        {
            _world.Events.Subscribe<EntityDeathEvent>(OnEnemyDeath);
            _deathEventSubscribed = true;
        }

        // Validate spawn points
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
            // Check enemy count before spawning
            int currentCount = GetCurrentEnemyCount();
            if (currentCount >= maxConcurrentEnemies)
            {
                // Wait until enemies die before continuing
                while (GetCurrentEnemyCount() >= maxConcurrentEnemies)
                {
                    yield return new WaitForSeconds(0.5f);
                }
            }

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
            int currentCount = GetCurrentEnemyCount();
            if (currentCount < maxConcurrentEnemies)
            {
                // Reduce spawn count to prevent overflow - spawn 1-2 at a time instead of 1-4
                int spawnCount = UnityEngine.Random.Range(1, 3); // Reduced from 1-4
                int remainingSlots = maxConcurrentEnemies - currentCount;
                spawnCount = Mathf.Min(spawnCount, remainingSlots);

                for (int i = 0; i < spawnCount; i++)
                {
                    // Double-check before each spawn
                    if (GetCurrentEnemyCount() >= maxConcurrentEnemies)
                    {
                        break;
                    }

                    Vector3 spawnPos = GetSpawnPosition();
                    EnemyDefinitionSO enemyData = config.enemyTypes[
                        UnityEngine.Random.Range(0, config.enemyTypes.Count)
                    ];
                    SpawnEnemy(enemyData, spawnPos, config);
                    yield return new WaitForSeconds(0.5f);
                }
            }
            // Increase interval between spawn waves to reduce pressure
            yield return new WaitForSeconds(continuousSpawnInterval * 1.5f);
        }
    }

    private void SpawnEnemy(EnemyDefinitionSO enemyData, Vector3 spawnPos, WaveConfiguration config)
    {
        // Check actual enemy count before spawning
        if (GetCurrentEnemyCount() >= maxConcurrentEnemies)
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
    }

    private int GetCurrentEnemyCount()
    {
        if (_world == null)
        {
            return 0;
        }

        int count = 0;
        foreach (var (entity, enemy, health) in _world.Components.Query<EnemyComponent, HealthDataComponent>())
        {
            if (!health.IsDead && enemy.CurrentState != EnemyState.Dead && !enemy.IsBoss)
            {
                count++;
            }
        }
        return count;
    }

    private void OnEnemyDeath(EntityDeathEvent @event)
    {
        // This is just for logging/debugging - actual count is queried dynamically
        if (_world.Components.Has<EnemyComponent>(@event.Entity))
        {
            Debug.Log($"[WaveManager] Enemy died. Current count: {GetCurrentEnemyCount()}");
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

        // Unsubscribe from death events
        if (_deathEventSubscribed && _world != null)
        {
            _world.Events.Unsubscribe<EntityDeathEvent>(OnEnemyDeath);
            _deathEventSubscribed = false;
        }
    }

    private void OnDestroy()
    {
        // Cleanup subscription
        if (_deathEventSubscribed && _world != null)
        {
            _world.Events.Unsubscribe<EntityDeathEvent>(OnEnemyDeath);
            _deathEventSubscribed = false;
        }
    }

    private WaveConfiguration GetWaveConfig(int round)
    {
        if (round - 1 < waveConfigs.Count)
        {
            return waveConfigs[round - 1];
        }

        // Cap enemy count growth after round 2 to prevent overflow and lag
        // Use a more conservative scaling that caps at a reasonable maximum
        int baseEnemyCount = 5;
        int scaledEnemyCount = baseEnemyCount + (round * 3); // Reduced from * 5
        int maxEnemyCount = 25; // Cap at 25 enemies per wave (below maxConcurrentEnemies of 20)
        int enemyCount = Mathf.Min(scaledEnemyCount, maxEnemyCount);

        return new WaveConfiguration
        {
            round = round,
            enemyCount = enemyCount,
            spawnInterval = Mathf.Max(0.3f, 0.5f - (round * 0.05f)), // Slower spawn rate
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
        
        // Animation parameters
        target.isMovingParam = source.isMovingParam;
        target.isRunningParam = source.isRunningParam;
        target.moveXParam = source.moveXParam;
        target.moveYParam = source.moveYParam;
        target.totalAttackAnimations = source.totalAttackAnimations;
        target.attackAnimationTrigger = source.attackAnimationTrigger;
        target.takeCoverParam = source.takeCoverParam;
        
        // Patrol settings
        target.generatePatrolPoints = source.generatePatrolPoints;
        target.patrolPointCount = source.patrolPointCount;
        target.patrolRadius = source.patrolRadius;
        
        // AI Behavior
        target.defaultState = source.defaultState;
        
        // Audio - CRITICAL: This was missing!
        target.audioProfile = source.audioProfile;
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
