using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class BossFactory
{
    private readonly World _world;

    public BossFactory(World world)
    {
        _world = world;
    }

    public GameObject CreateNetworkBoss(BossDefinitionSO bossData, Vector3 spawnPosition, out EntityId entity)
    {
        entity = default;

        if (!NetworkManager.Singleton.IsServer)
        {
            Debug.LogError("[BossFactory]: Only server can spawn bosses");
            return null;
        }

        if (bossData.prefab == null)
        {
            Debug.LogError($"[BossFactory] Boss prefab is null for {bossData.bossName}");
            return null;
        }

        if (bossData.prefab.GetComponent<NetworkObject>() == null)
        {
            Debug.LogError($"[BossFactory] Prefab {bossData.prefab.name} missing NetworkObject!");
            return null;
        }

        if (bossData.prefab.GetComponent<EnemyNetworkSyncView>() == null)
        {
            Debug.LogError($"[BossFactory] Prefab {bossData.prefab.name} missing EnemyNetworkSyncView!");
            return null;
        }

        // Spawn the boss
        GameObject bossObj = NetworkObjectSpawner.SpawnNewNetworkObject(bossData.prefab, spawnPosition, true);
        entity = _world.CreateEntity();

        // Bind all views
        foreach (EntityView view in bossObj.GetComponentsInChildren<EntityView>(includeInactive: true))
        {
            view.Bind(_world, entity);
            var registry = _world.Services.Resolve<EntityViewRegistry>();
            registry.Register(view);
        }

        // Transform component
        _world.Components.Add(entity, new TransformComponent(spawnPosition, Quaternion.identity));

        // Movement
        _world.Components.Add(entity, new MovementDataComponent { MoveSpeed = bossData.moveSpeed, IsMoving = false });

        // Health
        _world.Components.Add(
            entity,
            new HealthDataComponent
            {
                MaxHealth = bossData.maxHealth,
                CurrentHealth = bossData.maxHealth,
                IsDead = false,
            }
        );

        // Animation
        _world.Components.Add(
            entity,
            new AnimationDataComponent
            {
                IsMovingParam = bossData.isMovingParam,
                IsRunningParam = bossData.isRunningParam,
                MoveXParam = bossData.moveXParam,
                MoveYParam = bossData.moveYParam,
                AttackTrigger = bossData.hammerAnimationTrigger,
            }
        );

        // Enemy component (for AI state machine)
        EnemyComponent enemy = new EnemyComponent
        {
            IsBoss = true,
            IsRanged = false,
            DetectionRange = bossData.detectionRange,
            LoseTargetRange = bossData.loseTargetRange,
            FieldOfView = bossData.fieldOfView,
            CheckInterval = bossData.checkInterval,
            DetectionMask = bossData.detectionMask,
            CurrentState = EnemyState.Idle,
            StateTime = 0f,
            TargetEntity = default,
            Path = new List<Vector3>(),
            WaypointIndex = 0,
            LastRequestedTarget = Vector3.positiveInfinity,
            LastRequestTime = 0f,
            RequestCooldown = 0.3f,
            StoppingDistance = 0.5f,
            LastAgentPosition = spawnPosition,
            PatrolIndex = 0,
            PatrolWaypoints = new List<Vector3>(),
            CoverCooldown = 999f, // Boss doesn't take cover
        };
        _world.Components.Add(entity, enemy);

        // Boss component (for boss-specific abilities)
        BossComponent boss = new BossComponent
        {
            BossName = bossData.bossName,
            EnrageHealthThreshold = bossData.enrageHealthThreshold,

            // Jump Attack
            JumpAttackRange = bossData.jumpAttackRange,
            JumpAttackMinRange = bossData.jumpAttackMinRange,
            JumpAttackCooldown = bossData.jumpAttackCooldown,
            JumpAttackDamage = bossData.jumpAttackDamage,
            JumpAttackRadius = bossData.jumpAttackRadius,
            JumpDuration = bossData.jumpDuration,
            JumpLandingVFXPrefab = bossData.jumpLandingVFX,

            // Flamethrower
            FlamethrowerCooldown = bossData.flamethrowerCooldown,
            FlamethrowerDamagePerTick = bossData.flamethrowerDamagePerTick,
            FlamethrowerTickInterval = bossData.flamethrowerTickInterval,
            FlamethrowerRange = bossData.flamethrowerRange,
            FlamethrowerAngle = bossData.flamethrowerAngle,
            FlamethrowerDuration = bossData.flamethrowerDuration,
            FlamethrowerVFXPrefab = bossData.flamethrowerVFX,

            // Hammer Slam
            HammerSlamCooldown = bossData.hammerSlamCooldown,
            HammerSlamDamage = bossData.hammerSlamDamage,
            HammerSlamRadius = bossData.hammerSlamRadius,
            HammerSlamVFXPrefab = bossData.hammerSlamVFX,
            
            // Audio Clips
            JumpSound = bossData.jumpSound,
            FlamethrowerSound = bossData.flamethrowerSound,
            HammerSwingSound = bossData.hammerSwingSound,
            HammerSlamSound = bossData.hammerSlamSound,
        };
        _world.Components.Add(entity, boss);

        // Attack & Weapon (hammer)
        _world.Components.Add(entity, new AttackDataComponent { IsPlayerControlled = false });
        _world.Components.Add(
            entity,
            new WeaponDataComponent
            {
                WeaponName = "Giant Hammer",
                ExecutionType = AttackExecutionType.Melee,
                BaseDamage = bossData.hammerDamage,
                BaseCooldown = bossData.hammerCooldown,
                BaseRange = bossData.hammerRange,
                HitImpactParticlePrefab = bossData.hammerImpactVFX,
                AttackAnimationTrigger = bossData.hammerAnimationTrigger,
                TotalAttackAnimations = bossData.totalHammerAnimations,
                AttackSound = bossData.hammerSwingSound,
            }
        );

        // Network components
        NetworkObject netObj = bossObj.GetComponent<NetworkObject>();
        EnemyNetworkSyncView syncView = bossObj.GetComponent<EnemyNetworkSyncView>();

        if (syncView != null)
        {
            syncView.Initialize(_world, entity);
        }

        _world.Components.Add(entity, new NetworkSyncComponent { });
        _world.Components.Add(entity, new NetworkObjectComponent { NetworkObject = netObj });
        _world.Components.Add(entity, new NetworkOwnerComponent { ClientId = 0, IsLocalPlayer = false });

        // Audio profile
        if (bossData.audioProfile != null)
        {
            _world.Components.Add(entity, new AudioProfileComponent { Profile = bossData.audioProfile });
        }

        bossObj.name = $"{bossData.bossName}_Boss_{entity.Id}";

        Debug.Log($"[BossFactory] Spawned boss '{bossData.bossName}' with entity {entity.Id}");

        return bossObj;
    }
}
