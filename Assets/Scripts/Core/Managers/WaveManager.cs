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
    private int maxConcurrentEnemies = 15;

    private bool _deathEventSubscribed = false;

    [SerializeField]
    private List<WaveConfiguration> waveConfigs = new();

    [Header("Boss Configuration")]
    [SerializeField]
    private BossDefinitionSO bossDefinition;

    [SerializeField]
    private int bossSpawnRound = 2;

    [SerializeField]
    private float bossFightTimeLimit = 120f; // 2 minutes max for boss fight

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
    private BossFactory _bossFactory;
    private Coroutine _continuousSpawnCoroutine;
    private Coroutine _bossFightTimerCoroutine;
    private bool _bossSpawned = false;
    private EntityId _bossEntityId;

    public event System.Action OnBossFightTimeout;

    private void Start()
    {
        _world = WorldRunner.Instance.World;

        // Only get SpawnSystem on server (it's server-only now)
        if (NetworkManager.Singleton?.IsServer == true)
        {
            var field = typeof(WorldRunner).GetField(
                "_spawnSystem",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
            );
            _spawnSystem = field?.GetValue(WorldRunner.Instance) as SpawnSystem;

            if (_spawnSystem == null)
            {
                Debug.LogError("[Wave Manager] Failed to get SpawnSystem");
            }

            // Initialize BossFactory
            _bossFactory = new BossFactory(_world);
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
    }

    public void SpawnWave(int round)
    {
        if (!NetworkManager.Singleton.IsServer)
        {
            return;
        }

        // Check if this is a boss round
        if (round % bossSpawnRound == 0 && bossDefinition != null)
        {
            SpawnBoss();
            // return; // Boss round = boss only, no regular enemies
        }

        WaveConfiguration configuration = GetWaveConfig(round);

        if (configuration == null)
        {
            Debug.LogWarning($"[WaveManager]: No configuration for round {round}");
            return;
        }

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

    public void SpawnBoss()
    {
        if (_bossSpawned)
        {
            Debug.LogWarning("[WaveManager] Boss already spawned!");
            return;
        }

        if (bossDefinition == null)
        {
            Debug.LogError("[WaveManager] No boss definition assigned!");
            return;
        }

        // Get boss spawn position
        Vector3 spawnPos = Vector3.zero;
        if (bossSpawnPoints != null && bossSpawnPoints.Length > 0)
        {
            Transform spawnPoint = bossSpawnPoints[UnityEngine.Random.Range(0, bossSpawnPoints.Length)];
            spawnPos = spawnPoint.position;
        }
        else if (enemySpawnPoints != null && enemySpawnPoints.Length > 0)
        {
            // Fallback to enemy spawn points
            Transform spawnPoint = enemySpawnPoints[UnityEngine.Random.Range(0, enemySpawnPoints.Length)];
            spawnPos = spawnPoint.position;
        }

        // Spawn the boss
        _bossFactory.CreateNetworkBoss(bossDefinition, spawnPos, out EntityId bossEntity);
        _bossSpawned = true;
        _bossEntityId = bossEntity;

        // Start boss fight timer
        if (_bossFightTimerCoroutine != null)
        {
            StopCoroutine(_bossFightTimerCoroutine);
        }
        _bossFightTimerCoroutine = StartCoroutine(BossFightTimerCoroutine());

        Debug.Log($"[WaveManager] Boss '{bossDefinition.bossName}' spawned at {spawnPos}! Time limit: {bossFightTimeLimit}s");
    }

    private IEnumerator BossFightTimerCoroutine()
    {
        yield return new WaitForSeconds(bossFightTimeLimit);

        // Time's up! Kill the boss and end the fight
        if (_bossSpawned && _world != null)
        {
            Debug.Log("[WaveManager] Boss fight timeout! Ending boss round.");

            // Force kill the boss
            if (_world.Components.TryGet(_bossEntityId, out HealthDataComponent health))
            {
                health.CurrentHealth = 0;
                health.IsDead = true;
                _world.Events.Publish(new EntityDeathEvent { Entity = _bossEntityId });
            }

            OnBossFightTimeout?.Invoke();
        }
    }

    public void ResetBossSpawned()
    {
        _bossSpawned = false;
        _bossEntityId = default;

        if (_bossFightTimerCoroutine != null)
        {
            StopCoroutine(_bossFightTimerCoroutine);
            _bossFightTimerCoroutine = null;
        }
    }

    public bool IsBossAlive()
    {
        if (!_bossSpawned) return false;

        if (_world.Components.TryGet(_bossEntityId, out HealthDataComponent health))
        {
            return !health.IsDead && health.CurrentHealth > 0;
        }
        return false;
    }

    private IEnumerator SpawnWaveCoroutine(WaveConfiguration config)
    {
        for (int i = 0; i < config.enemyCount; i++)
        {
            // Check enemy count before spawning
            int currentCount = GetCurrentEnemyCount();

            if (currentCount >= maxConcurrentEnemies)
            {
                Debug.Log($"[WaveManager] Max enemies reached, waiting for enemies to die...");
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
            // Enemy death counted
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
            return pos;
        }

        // Try to spawn near players
        Vector3 playerPos = FindNearestPlayerPosition();

        // If no valid player position found, use random spawn point
        if (playerPos == Vector3.zero || float.IsNaN(playerPos.x) || float.IsNaN(playerPos.z))
        {
            int fallbackIndex = UnityEngine.Random.Range(0, enemySpawnPoints.Length);
            Vector3 fallbackPos = enemySpawnPoints[fallbackIndex].position;
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
            }
            else
            {
                // Grid position invalid, use closest spawn point
                spawnPos = GetClosestSpawnPoint(playerPos);
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

    /// <summary>
    /// Gets a random alive player's position for spawning enemies near.
    /// </summary>
    private Vector3 FindNearestPlayerPosition()
    {
        List<Vector3> alivePlayerPositions = new();

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

            alivePlayerPositions.Add(trans.Position);
        }

        if (alivePlayerPositions.Count == 0)
        {
            return Vector3.zero;
        }

        // Randomly select one of the alive players to spawn enemies near
        int randomIndex = UnityEngine.Random.Range(0, alivePlayerPositions.Count);
        return alivePlayerPositions[randomIndex];
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
            spawnInterval = Mathf.Max(0.3f, 0.5f - (round * 0.05f)),
            // Cap scaling to prevent extreme late-game damage:
            // Health: +10% per round, max +100%
            // Damage: +5% per round, max +20%
            // Speed: +5% per round, max +30%
            healthMultiplier = 1f + Mathf.Min(round * 0.1f, 1f),
            damageMultiplier = 1f + Mathf.Min(round * 0.05f, 0.2f),
            speedMultiplier = 1f + Mathf.Min(round * 0.05f, 0.3f),
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
        
        // Deep copy attacks to prevent modifying the original ScriptableObject
        target.attacks = new List<AttackDefinition>();
        foreach (var sourceAttack in source.attacks)
        {
            target.attacks.Add(new AttackDefinition
            {
                attackName = sourceAttack.attackName,
                executionType = sourceAttack.executionType,
                damage = sourceAttack.damage,
                cooldown = sourceAttack.cooldown,
                range = sourceAttack.range,
                animationTrigger = sourceAttack.animationTrigger,
                totalAnimations = sourceAttack.totalAnimations,
                attackSound = sourceAttack.attackSound,
                hitImpactVFX = sourceAttack.hitImpactVFX,
                projectilePrefab = sourceAttack.projectilePrefab,
                projectileSpeed = sourceAttack.projectileSpeed,
                projectileLifetime = sourceAttack.projectileLifetime,
                projectileSpawnOffset = sourceAttack.projectileSpawnOffset
            });
        }

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

        // Audio
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
