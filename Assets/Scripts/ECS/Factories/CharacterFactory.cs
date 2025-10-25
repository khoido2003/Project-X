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

        // Action Flag
        _world.Components.Add(entity, new ActionFlagComponent { });

        // Player Tag
        _world.Components.Add(entity, new PlayerTagComponent { });

        // --- Transform ---
        _world.Components.Add(entity, new TransformComponent(instance.transform.position, Quaternion.identity));

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

        // Skills
        _world.Components.Add(entity, new SkillSetComponent(data.skills));

        // Skill Buffer
        _world.Components.Add(entity, new SkillCastBufferComponent());

        // Combat State
        _world.Components.Add(entity, new CombatStateComponent());

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
                    HitImpactParticlePrefab = data.weaponData.hitImpactParticlePrefab,
                    AttackAnimationTrigger = data.weaponData.attackAnimationTrigger,
                    TotalAttackAnimations = data.weaponData.totalAttackAnimations,
                    AttackSound = data.weaponData.attackSound,
                }
            );
        }

        instance.name = $"{data.characterName}_Entity{entity.Id}";
        return instance;
    }
}
