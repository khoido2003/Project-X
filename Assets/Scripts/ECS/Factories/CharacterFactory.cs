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

        // WeaponHolder weaponHolder = instance.GetComponentInChildren<WeaponHolder>();

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

        if (data.attacks != null && data.attacks.Count > 0)
        {
            var attack = data.attacks[0]; // Primary attack, or loop for multiple later

            _world.Components.Add(entity, new AttackDataComponent { IsPlayerControlled = data.isPlayer });

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
                }
            );
        }

        instance.name = $"{data.characterName}_Entity{entity.Id}";
        return instance;
    }
}
