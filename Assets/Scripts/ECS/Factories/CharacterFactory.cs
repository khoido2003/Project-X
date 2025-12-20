using Unity.Netcode;
using UnityEngine;

public class CharacterFactory
{
    private readonly World _world;

    public CharacterFactory(World world)
    {
        _world = world;
    }

    public GameObject CreateNetworkCharacter(
        CharacterDefinitionSO characterData,
        Vector3 spawnPosition,
        ulong clientId,
        out EntityId entity
    )
    {
        GameObject playerObj = Object.Instantiate(characterData.prefab, spawnPosition, Quaternion.identity);

        // Create entity in World
        entity = _world.CreateEntity();

        // Bind all views
        foreach (EntityView view in playerObj.GetComponentsInChildren<EntityView>(includeInactive: true))
        {
            view.Bind(_world, entity);
            var registry = _world.Services.Resolve<EntityViewRegistry>();
            registry.Register(view);
        }

        var attackExecView = playerObj.GetComponent<AttackExecutionView>();
        if (attackExecView == null)
        {
            attackExecView = playerObj.AddComponent<AttackExecutionView>();
            attackExecView.Bind(_world, entity);
            var registry = _world.Services.Resolve<EntityViewRegistry>();
            registry.Register(attackExecView);
            Debug.Log($"[CharacterFactory] Added AttackExecutionView to entity {entity.Id}");
        }

        // Get or add NetworkSyncView
        var networkSync = playerObj.GetComponent<NetworkSyncView>();
        if (networkSync == null)
        {
            Debug.LogError(
                $"[CharacterFactory] Prefab {characterData.characterName} is missing NetworkSyncView component!"
            );
            Object.Destroy(playerObj);
            entity = default;
            return null;
        }

        // Get NetworkObject component
        NetworkObject netObj = playerObj.GetComponent<NetworkObject>();
        if (netObj == null)
        {
            Debug.LogError(
                $"[CharacterFactory] Prefab {characterData.characterName} is missing NetworkObject component!"
            );
            Object.Destroy(playerObj);
            entity = default;
            return null;
        }

        Debug.Log(
            $"[CharacterFactory] NetworkSyncView instance: {networkSync.GetInstanceID()}, NetworkObject: {netObj.GetInstanceID()}"
        );

        // Initialize BEFORE spawning the NetworkObject
        // This must be called on the server BEFORE SpawnWithOwnership
        networkSync.Initialize(_world, entity);
        Debug.Log(
            $"[CharacterFactory] Called Initialize() on NetworkSyncView for entity {entity.Id}, client {clientId}"
        );

        // Add network components
        _world.Components.Add(entity, new NetworkSyncComponent { SyncView = networkSync });
        _world.Components.Add(entity, new NetworkObjectComponent { NetworkObject = netObj });

        _world.Components.Add(
            entity,
            new NetworkOwnerComponent
            {
                ClientId = clientId,
                IsLocalPlayer = clientId == NetworkManager.Singleton.LocalClientId,
            }
        );

        _world.Components.Add(entity, new CharacterSelectionComponent { CharacterData = characterData });

        // Standard component
        _world.Components.Add(entity, new ActionFlagComponent());

        _world.Components.Add(entity, new PlayerTagComponent());

        _world.Components.Add(entity, new TransformComponent(spawnPosition, Quaternion.identity));

        // Health
        _world.Components.Add(
            entity,
            new HealthDataComponent { MaxHealth = characterData.maxHealth, CurrentHealth = characterData.maxHealth }
        );

        // Movement
        _world.Components.Add(
            entity,
            new MovementDataComponent
            {
                MoveSpeed = characterData.moveSpeed,
                ForwardMultiplier = characterData.forwardMultiplier,
                IsPlayerControlled = true,
            }
        );

        // Animation
        _world.Components.Add(
            entity,
            new AnimationDataComponent
            {
                IsMovingParam = characterData.isMovingParam,
                MoveXParam = characterData.moveXParam,
                MoveYParam = characterData.moveYParam,
            }
        );

        // Audio profile - always add component, even if null, for better error tracking
        _world.Components.Add(entity, new AudioProfileComponent { Profile = characterData.audioProfile });

        if (characterData.audioProfile == null)
        {
            Debug.LogWarning(
                $"[CharacterFactory] Character '{characterData.characterName}' (Entity {entity.Id}) does not have an AudioProfile assigned in CharacterDefinitionSO. Audio cues will not play."
            );
        }

        // Skills
        _world.Components.Add(entity, new SkillSetComponent(characterData.skills));
        _world.Components.Add(entity, new SkillCastBufferComponent());

        // Combat
        _world.Components.Add(entity, new CombatStateComponent());

        // Attack
        if (characterData.attacks != null && characterData.attacks.Count > 0)
        {
            var attack = characterData.attacks[0];

            _world.Components.Add(entity, new AttackDataComponent { IsPlayerControlled = true });
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
                    ProjectileLifetime = attack.projectileLifetime,
                    ProjectileSpawnOffset = attack.projectileSpawnOffset,
                    ProjectileSpeed = attack.projectileSpeed,
                }
            );
            // Note: Client-side weapon data comes from prefab's serialized fields
        }

        // Game State Component
        _world.Components.Add(entity, new PlayerScoreComponent { });
        _world.Components.Add(entity, new PlayerRespawnComponent { OriginalSpawnPosition = spawnPosition });
        _world.Components.Add(entity, new PlayerUpgradesComponent { });

        // NOW spawn the NetworkObject and change ownership
        // This triggers OnNetworkSpawn, but Initialize() has already been called
        Debug.Log($"[CharacterFactory] About to spawn NetworkObject for entity {entity.Id}, client {clientId}");
        netObj.SpawnWithOwnership(clientId);
        Debug.Log(
            $"[CharacterFactory] Successfully spawned NetworkObject {netObj.NetworkObjectId} for entity {entity.Id}, client {clientId}"
        );

        return playerObj;
    }

    // ============================
    // PLAYER CHARACTER CREATION TEST MODE
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

        // Audio profile - always add component, even if null, for better error tracking
        _world.Components.Add(entity, new AudioProfileComponent { Profile = data.audioProfile });

        if (data.audioProfile == null)
        {
            Debug.LogWarning(
                $"[CharacterFactory] Character '{data.characterName}' (Entity {entity.Id}) does not have an AudioProfile assigned in CharacterDefinitionSO. Audio cues will not play."
            );
        }

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
