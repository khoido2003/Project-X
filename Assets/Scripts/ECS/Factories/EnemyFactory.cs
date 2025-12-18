using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class EnemyFactory
{
    private readonly World _world;

    public EnemyFactory(World world)
    {
        _world = world;
    }

    public GameObject CreateNetworkEnemy(EnemyDefinitionSO enemyData, Vector3 spawnPosition, out EntityId entity)
    {
        entity = default;
        if (!NetworkManager.Singleton.IsServer)
        {
            Debug.LogError("[EnemyFactory]: Only server can spawn enemies");
            return null;
        }

        if (enemyData.prefab == null)
        {
            Debug.LogError($"[EnemyFactory] Enemy prefab is null for {enemyData.enemyName}");
            return null;
        }

        if (enemyData.prefab.GetComponent<NetworkObject>() == null)
        {
            Debug.LogError($"[EnemyFactory] Prefab {enemyData.prefab.name} does not have NetworkObject component!");
            return null;
        }

        GameObject enemyObj = NetworkObjectSpawner.SpawnNewNetworkObject(enemyData.prefab, spawnPosition, true);
        NetworkObject netObj = enemyObj.GetComponent<NetworkObject>();
        entity = _world.CreateEntity();

        foreach (EntityView view in enemyObj.GetComponentsInChildren<EntityView>(includeInactive: true))
        {
            view.Bind(_world, entity);
            var registry = _world.Services.Resolve<EntityViewRegistry>();
            registry.Register(view);
        }

        var attackExecView = enemyObj.GetComponent<AttackExecutionView>();
        if (attackExecView == null)
        {
            attackExecView = enemyObj.AddComponent<AttackExecutionView>();
            attackExecView.Bind(_world, entity);
            var registry = _world.Services.Resolve<EntityViewRegistry>();
            registry.Register(attackExecView);
            Debug.Log($"[EnemyFactory] Added AttackExecutionView to enemy entity {entity.Id}");
        }

        var networkSync = enemyObj.GetComponent<EnemyNetworkSyncView>();
        if (networkSync == null)
        {
            networkSync = enemyObj.AddComponent<EnemyNetworkSyncView>();
        }
        networkSync.Initialize(_world, entity);

        // Note: EnemyNetworkSyncView doesn't inherit from NetworkSyncView
        _world.Components.Add(entity, new NetworkSyncComponent { SyncView = null });

        _world.Components.Add(entity, new NetworkObjectComponent { NetworkObject = netObj });
        _world.Components.Add(
            entity,
            new EnemyNetworkComponent { SpawnerId = netObj.NetworkObjectId, IsNetworked = true }
        );

        // Transform
        _world.Components.Add(entity, new TransformComponent(spawnPosition, enemyObj.transform.rotation));

        // Health
        _world.Components.Add(
            entity,
            new HealthDataComponent { MaxHealth = enemyData.maxHealth, CurrentHealth = enemyData.maxHealth }
        );

        // Movement
        _world.Components.Add(
            entity,
            new MovementDataComponent
            {
                MoveSpeed = enemyData.moveSpeed,
                ForwardMultiplier = 1f,
                IsPlayerControlled = false,
            }
        );

        // Animation
        _world.Components.Add(
            entity,
            new AnimationDataComponent
            {
                IsMovingParam = enemyData.isMovingParam,
                IsRunningParam = enemyData.isRunningParam,
                MoveXParam = enemyData.moveXParam,
                MoveYParam = enemyData.moveYParam,
                AttackTrigger = enemyData.attackAnimationTrigger,
                TakeCoverParam = enemyData.takeCoverParam,
            }
        );

        if (enemyData.audioProfile == null)
        {
            Debug.LogWarning(
                $"[EnemyFactory] Enemy '{enemyData.enemyName}' (Entity {entity.Id}) does not have an AudioProfile assigned in EnemyDefinitionSO. "
                    + $"EnemyDefinitionSO name: '{enemyData.name}', Asset path: Check the Inspector to verify the audioProfile field is assigned. Audio cues will not play."
            );
        }
        else
        {
            Debug.Log(
                $"[EnemyFactory] Enemy '{enemyData.enemyName}' (Entity {entity.Id}) has AudioProfile: '{enemyData.audioProfile.name}'"
            );
        }

        _world.Components.Add(entity, new AudioProfileComponent { Profile = enemyData.audioProfile });

        // Enemy AI Component
        EnemyComponent enemy = new EnemyComponent
        {
            IsRanged = enemyData.isRanged,
            DetectionRange = enemyData.detectionRange,
            LoseTargetRange = enemyData.loseTargetRange,
            FieldOfView = enemyData.fieldOfView,
            CheckInterval = enemyData.checkInterval,
            TimeSinceLastCheck = 0f,
            DetectionMask = enemyData.detectionMask,
            CurrentState = EnemyState.Idle,
            StateTime = 0f,
            TargetEntity = default,
            Path = new List<Vector3>(),
            WaypointIndex = 0,
            LastRequestedTarget = Vector3.positiveInfinity,
            LastRequestTime = 0f,
            RequestCooldown = 0.5f,
            StoppingDistance = 0.5f,
            LastAgentPosition = spawnPosition,
            NoProgressTimer = 0f,
            StuckTimer = 0f,
            PatrolIndex = 0,
            PatrolWaypoints = new List<Vector3>(),
        };

        if (enemyData.generatePatrolPoints)
        {
            enemy.PatrolWaypoints.AddRange(
                GeneratePatrolPointsAround(spawnPosition, enemyData.patrolPointCount, enemyData.patrolRadius)
            );
            Debug.Log(
                $"[EnemyFactory] Entity {entity.Id}: Generated {enemy.PatrolWaypoints.Count} patrol waypoints (radius: {enemyData.patrolRadius})"
            );
        }
        else
        {
            // FALLBACK: Always generate at least some patrol points so enemies aren't stuck in Idle
            enemy.PatrolWaypoints.AddRange(GeneratePatrolPointsAround(spawnPosition, 4, 5f));
            Debug.Log(
                $"[EnemyFactory] Entity {entity.Id}: GeneratePatrolPoints=false, added fallback {enemy.PatrolWaypoints.Count} patrol waypoints"
            );
        }
        _world.Components.Add(entity, enemy);

        // Attack & Weapon
        if (enemyData.attacks != null && enemyData.attacks.Count > 0)
        {
            var attack = enemyData.attacks[0];

            _world.Components.Add(entity, new AttackDataComponent { IsPlayerControlled = false });

            _world.Components.Add(
                entity,
                new WeaponDataComponent
                {
                    WeaponName = attack.attackName,
                    ExecutionType = attack.executionType,
                    BaseDamage = attack.damage,
                    BaseCooldown = attack.cooldown,
                    BaseRange = attack.range,
                    HitImpactParticlePrefab = attack.hitImpactVFX,
                    AttackAnimationTrigger = attack.animationTrigger,
                    TotalAttackAnimations = attack.totalAnimations,
                    AttackSound = attack.attackSound,
                    ProjectilePrefab = attack.projectilePrefab,
                    ProjectileSpeed = attack.projectileSpeed,
                    ProjectileLifetime = attack.projectileLifetime,
                    ProjectileSpawnOffset = attack.projectileSpawnOffset,
                }
            );

            networkSync.SetWeaponData(
                attack.executionType,
                attack.projectilePrefab,
                attack.hitImpactVFX,
                attack.projectileSpeed,
                attack.projectileLifetime,
                attack.projectileSpawnOffset,
                attack.animationTrigger
            );
        }

        enemyObj.name = $"{enemyData.enemyName}_Entity{entity.Id}";

        Debug.Log($"[EnemyFactory] Spawned network enemy {enemyData.enemyName} at {spawnPosition}");

        return enemyObj;
    }

    // ============================
    // ENEMY CREATION TEST MODE
    // ============================
    public GameObject CreateEnemy(EnemyDefinitionSO data, Vector3 spawnPos, out EntityId entity)
    {
        GameObject instance = Object.Instantiate(data.prefab, spawnPos, Quaternion.identity);

        entity = _world.CreateEntity();

        foreach (EntityView view in instance.GetComponentsInChildren<EntityView>(includeInactive: true))
        {
            view.Bind(_world, entity);
            var registry = _world.Services.Resolve<EntityViewRegistry>();
            registry.Register(view);
        }

        // Transform
        _world.Components.Add(entity, new TransformComponent(spawnPos, instance.transform.rotation));

        // Movement
        _world.Components.Add(entity, new MovementDataComponent { MoveSpeed = data.moveSpeed });

        // --- Health ---
        _world.Components.Add(
            entity,
            new HealthDataComponent { MaxHealth = data.maxHealth, CurrentHealth = data.maxHealth }
        );

        // --- Animation ---
        _world.Components.Add(
            entity,
            new AnimationDataComponent
            {
                IsMovingParam = data.isMovingParam,
                MoveXParam = data.moveXParam,
                MoveYParam = data.moveYParam,
                AttackTrigger = data.attackAnimationTrigger,
                IsRunningParam = data.isRunningParam,
                TakeCoverParam = data.takeCoverParam,
            }
        );

        // Audio profile - always add component, even if null, for better error tracking
        _world.Components.Add(entity, new AudioProfileComponent { Profile = data.audioProfile });

        if (data.audioProfile == null)
        {
            Debug.LogWarning(
                $"[EnemyFactory] Enemy '{data.enemyName}' (Entity {entity.Id}) does not have an AudioProfile assigned in EnemyDefinitionSO. Audio cues will not play."
            );
        }

        // --- Attack Data (needed for AnimationEventRelayView) ---
        _world.Components.Add(
            entity,
            new AttackDataComponent { IsPlayerControlled = false, AttackSpeedMultiplier = 1f }
        );

        EnemyComponent enemy = new EnemyComponent
        {
            IsRanged = data.isRanged,

            // Vision & Detection
            DetectionRange = data.detectionRange,
            LoseTargetRange = data.loseTargetRange,
            FieldOfView = data.fieldOfView,
            CheckInterval = data.checkInterval,
            TimeSinceLastCheck = 0f,
            DetectionMask = data.detectionMask,

            // AI FSM
            CurrentState = EnemyState.Idle,
            StateTime = 0f,
            TargetEntity = default,

            // Pathfinding
            Path = new List<Vector3>(),
            WaypointIndex = 0,
            LastRequestedTarget = Vector3.positiveInfinity,
            LastRequestTime = 0f,
            RequestCooldown = 0.5f,
            StoppingDistance = 0.5f,
            LastAgentPosition = spawnPos,
            NoProgressTimer = 0f,
            StuckTimer = 0f,

            // Patrol
            PatrolIndex = 0,
            PatrolWaypoints = new List<Vector3>(),
        };

        // --- Generate patrol points if needed ---
        if (data.generatePatrolPoints)
        {
            enemy.PatrolWaypoints.AddRange(
                GeneratePatrolPointsAround(spawnPos, data.patrolPointCount, data.patrolRadius)
            );
        }

        // --- Register unified component ---
        _world.Components.Add(entity, enemy);

        // --- Attack + Weapon ---
        if (data.attacks != null && data.attacks.Count > 0)
        {
            var attack = data.attacks[0];

            _world.Components.Add(
                entity,
                new WeaponDataComponent
                {
                    WeaponName = attack.attackName,
                    ExecutionType = attack.executionType,
                    BaseDamage = attack.damage,
                    BaseCooldown = attack.cooldown,
                    BaseRange = attack.range,
                    HitImpactParticlePrefab = attack.hitImpactVFX,
                    AttackAnimationTrigger = attack.animationTrigger,
                    TotalAttackAnimations = attack.totalAnimations,
                    AttackSound = attack.attackSound,
                    ProjectilePrefab = attack.projectilePrefab,
                    ProjectileSpawnOffset = attack.projectileSpawnOffset,
                }
            );
        }

        instance.name = $"{data.enemyName}_Enemy_{entity.Id}";
        return instance;
    }

    private List<Vector3> GeneratePatrolPointsAround(Vector3 spawnPos, int cnt, float radius)
    {
        List<Vector3> list = new(cnt);

        if (cnt <= 0)
        {
            return list;
        }

        GridSystem grid = GridSystem.Instance;
        var rng = new System.Random(spawnPos.GetHashCode());
        float minDistance = radius * 0.5f;

        int attempts = 0;
        while (list.Count < cnt && attempts < cnt * 6)
        {
            attempts++;

            float ang = (float)(rng.NextDouble() * Mathf.PI * 2f);
            float dist = (float)(rng.NextDouble() * radius * 0.8f + radius * 0.2f);

            Vector3 candicate = spawnPos + new Vector3(Mathf.Cos(ang) * dist, 0f, Mathf.Sin(ang) * dist);

            Vector2Int gridPos = grid.GetGridPosition(candicate);
            gridPos = grid.GetGridPosition(candicate);
            gridPos = grid.FindNearestWalkable(gridPos);
            Vector3 snapped = grid.GetWorldPosition(gridPos);

            snapped.y = spawnPos.y;

            bool tooCLose = false;
            foreach (var p in list)
            {
                if ((p - snapped).sqrMagnitude < minDistance * minDistance)
                {
                    tooCLose = true;
                    break;
                }
            }
            if (!tooCLose)
            {
                list.Add(snapped);
            }
        }

        // Fallback if the random points are not enough then spawn new one with some offsets

        for (int i = list.Count; i < cnt; i++)
        {
            list.Add(spawnPos + new Vector3(i + 1f, 0f, 0f));
        }

        return list;
    }
}
