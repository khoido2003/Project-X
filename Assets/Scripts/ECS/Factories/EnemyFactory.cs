using System.Collections.Generic;
using UnityEngine;

public class EnemyFactory
{
    private readonly World _world;

    public EnemyFactory(World world)
    {
        _world = world;
    }

    // ============================
    // ENEMY CREATION
    // ============================
    public GameObject CreateEnemy(EnemyDefinitionSO data, Vector3 spawnPos)
    {
        GameObject instance = Object.Instantiate(data.prefab, spawnPos, Quaternion.identity);

        EntityId entity = _world.CreateEntity();

        foreach (EntityView view in instance.GetComponentsInChildren<EntityView>(includeInactive: true))
        {
            view.Bind(_world, entity);
            var registry = _world.Services.Resolve<EntityViewRegistry>();
            registry.Register(view);
        }

        // Transform
        _world.Components.Add(entity, new TransformComponent(spawnPos, instance.transform.rotation));

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

        // --- Attack Data (needed for AnimationEventRelayView) ---
        _world.Components.Add(
            entity,
            new AttackDataComponent { IsPlayerControlled = false, AttackSpeedMultiplier = 1f }
        );

        EnemyComponent enemy = new EnemyComponent
        {
            // Stats
            MaxHealth = data.maxHealth,
            CurrentHealth = data.maxHealth,
            MoveSpeed = data.moveSpeed,
            AttackRange = data.attackRange,
            AttackCooldown = data.attackCooldown,
            Damage = data.damage,
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
