using UnityEngine;

public class CharacterFactory
{
    private readonly World _world;

    public CharacterFactory(World world)
    {
        _world = world;
    }

    // ============================
    // PLAYER CHARACTER CREATION
    // ============================
    public GameObject CreateCharacter(CharacterDefinitionSO data, Vector3 spawnPos)
    {
        GameObject instance = Object.Instantiate(data.prefab, spawnPos, Quaternion.identity);

        WeaponHolder weaponHolder = instance.GetComponentInChildren<WeaponHolder>();

        EntityId entity = _world.CreateEntity();

        foreach (EntityView view in instance.GetComponentsInChildren<EntityView>(includeInactive: true))
        {
            view.Bind(_world, entity);
            var registry = _world.Services.Resolve<EntityViewRegistry>();
            registry.Register(view);
        }

        // --- Health ---
        _world.Components.Add(
            entity,
            new HealthDataComponent { MaxHealth = data.maxHealth, CurrentHealth = data.maxHealth }
        );

        // --- Movement ---
        _world.Components.Add(
            entity,
            new MovementDataComponent
            {
                MoveSpeed = data.moveSpeed,
                ForwardMultiplier = data.forwardMultiplier,
                IsPlayerControlled = data.isPlayer,
            }
        );

        // --- Animation ---
        _world.Components.Add(
            entity,
            new AnimationDataComponent
            {
                IsMovingParam = data.isMovingParam,
                MoveXParam = data.moveXParam,
                MoveYParam = data.moveYParam,
            }
        );

        // --- Attack + Weapon ---
        if (data.hasWeapon && data.weaponData != null)
        {
            _world.Components.Add(entity, new AttackDataComponent { IsPlayerControlled = data.isPlayer });

            _world.Components.Add(
                entity,
                new WeaponDataComponent
                {
                    WeaponName = data.weaponData.weaponName,
                    ExecutionType = data.weaponData.ExecutionType,
                    BaseDamage = data.weaponData.attackDamage,
                    BaseCooldown = data.weaponData.attackCooldown,
                    BaseRange = data.weaponData.attackRange,
                    WeaponPrefab = data.weaponData.weaponPrefab,
                    ProjectilePrefab = data.weaponData.projectilePrefab,
                    SpawnPositionOffset = data.weaponData.spawnPositionOffset,
                    SpawnRotationOffset = data.weaponData.spawnRotationOffset,
                    HitImpactParticlePrefab = data.weaponData.hitImpactParticlePrefab,
                    AttackAnimationTrigger = data.weaponData.attackAnimationTrigger,
                    TotalAttackAnimations = data.weaponData.totalAttackAnimations,
                    AttackSound = data.weaponData.attackSound,
                    WeaponHolder = weaponHolder?.transform,
                }
            );
        }

        instance.name = $"{data.characterName}_Entity{entity.Id}";
        return instance;
    }

    // ============================
    // ENEMY CREATION
    // ============================
    public GameObject CreateEnemy(CharacterDefinitionSO data, Vector3 spawnPos)
    {
        GameObject instance = Object.Instantiate(data.prefab, spawnPos, Quaternion.identity);

        EntityId entity = _world.CreateEntity();

        foreach (EntityView view in instance.GetComponentsInChildren<EntityView>(includeInactive: true))
        {
            view.Bind(_world, entity);
            var registry = _world.Services.Resolve<EntityViewRegistry>();
            registry.Register(view);
        }

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
            }
        );
        // --- Attack + Weapon ---
        if (data.hasWeapon && data.weaponData != null)
        {
            _world.Components.Add(entity, new AttackDataComponent { IsPlayerControlled = false });

            _world.Components.Add(
                entity,
                new WeaponDataComponent
                {
                    WeaponName = data.weaponData.weaponName,
                    ExecutionType = data.weaponData.ExecutionType,
                    BaseDamage = data.weaponData.attackDamage,
                    BaseCooldown = data.weaponData.attackCooldown,
                    BaseRange = data.weaponData.attackRange,
                    ProjectilePrefab = data.weaponData.projectilePrefab,
                    HitImpactParticlePrefab = data.weaponData.hitImpactParticlePrefab,
                    AttackAnimationTrigger = data.weaponData.attackAnimationTrigger,
                    TotalAttackAnimations = data.weaponData.totalAttackAnimations,
                }
            );
        }

        instance.name = $"{data.characterName}_Enemy_{entity.Id}";
        return instance;
    }
}
