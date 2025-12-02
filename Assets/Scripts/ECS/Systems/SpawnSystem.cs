using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class SpawnSystem : ISystem
{
    private readonly SpawnConfigSO _config;
    private readonly List<SpawnPoint> _spawnPoints = new();
    private CharacterFactory _characterFactory;
    private EnemyFactory _enemyFactory;
    private World _world;

    public SpawnSystem(SpawnConfigSO config)
    {
        _config = config;
    }

    public void Initialize(World world)
    {
        _world = world;
        _characterFactory = new CharacterFactory(world);
        _enemyFactory = new EnemyFactory(world);

        // _spawnPoints.AddRange(Object.FindObjectsByType<SpawnPoint>(FindObjectsSortMode.None));
        //
        // if (_spawnPoints.Count == 0)
        // {
        //     Debug.LogError("No spawn points found in scene!");
        //     return;
        // }
        //
        // SpawnPlayer();
        // SpawnEnemies();
    }

    public void Update(float dt) { }

    public void FixedUpdate(float dt) { }

    public void Shutdown()
    {
        _spawnPoints.Clear();
    }

    ////////////////////////////////////////////////////////////////

    // NETWORK MODE

    public void SpawnNetworkPlayer(ulong clientId, CharacterDefinitionSO characterData, Vector3 spawnPosition)
    {
        if (!NetworkManager.Singleton.IsServer)
        {
            Debug.LogError("Only server can spawn players!");
            return;
        }

        // Validate character data
        if (characterData == null)
        {
            Debug.LogError($"CharacterDefinitionSO is null for client {clientId}");
            return;
        }

        if (characterData.prefab == null)
        {
            Debug.LogError($"Character prefab is null for {characterData.characterName}");
            return;
        }

        // Validate prefab has NetworkObject
        if (characterData.prefab.GetComponent<NetworkObject>() == null)
        {
            Debug.LogError($"Prefab {characterData.prefab.name} doesn't have NetworkObject component!");
            return;
        }

        GameObject playerObj = NetworkObjectSpawner.SpawnNewNetworkObjectChangeOwnershipToClient(
            characterData.prefab,
            spawnPosition,
            clientId,
            true
        );

        EntityId entity = _world.CreateEntity();

        foreach (EntityView view in playerObj.GetComponentsInChildren<EntityView>(includeInactive: true))
        {
            view.Bind(_world, entity);
            var registry = _world.Services.Resolve<EntityViewRegistry>();
            registry.Register(view);
        }

        var networkSync = playerObj.GetComponent<NetworkSyncView>();
        if (networkSync == null)
        {
            networkSync = playerObj.AddComponent<NetworkSyncView>();
        }

        networkSync.Initialize(_world, entity);

        // Network component
        NetworkObject netObj = playerObj.GetComponent<NetworkObject>();

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
                }
            );
        }

        playerObj.name = $"{characterData.characterName}_Client{clientId}_Entity{entity.Id}";

        // Publish spawn event (for camera to follow if local player)
        _world.Events.Publish(new PlayerSpawnEvent(entity, playerObj, playerObj.transform));

        Debug.Log($"Spawned player for client {clientId} at {spawnPosition}");
    }

    /////////////////////////////////////////////////////////////////////////////

    // TEST MODE

    private void SpawnPlayer()
    {
        if (_spawnPoints.Count == 0)
        {
            _spawnPoints.AddRange(Object.FindObjectsByType<SpawnPoint>(FindObjectsSortMode.None));
        }

        if (_config == null)
        {
            Debug.LogError("SpawnConfigSO is null!");
            return;
        }

        List<SpawnPoint> playerSpawns = _spawnPoints.FindAll(s => s.type == SpawnType.Player);
        if (playerSpawns.Count == 0)
        {
            Debug.LogError("No player spawn points found!");
            return;
        }

        int maxPlayers = Mathf.Min(_config.maxPlayers, playerSpawns.Count);

        int characterCount = Mathf.Min(_config.possiblePlayers.Length, maxPlayers);

        var shuffleSpawns = new List<SpawnPoint>(playerSpawns);
        Shuffle(shuffleSpawns);

        for (int i = 0; i < characterCount; i++)
        {
            CharacterDefinitionSO data = _config.possiblePlayers[i];

            SpawnPoint spawn = shuffleSpawns[i];

            GameObject playerObj = _characterFactory.CreateCharacter(data, spawn.transform.position);

            EntityView view = playerObj.GetComponent<EntityView>();

            // Publish events
            _world.Events.Publish(new PlayerSpawnEvent(view.EntityInstance, playerObj, playerObj.transform));
        }
    }

    private void SpawnEnemies()
    {
        var enemyPoints = _spawnPoints.FindAll(s => s.type == SpawnType.Enemy);

        if (enemyPoints.Count == 0)
        {
            Debug.LogWarning("No Enemy Spawn points found!");

            return;
        }

        int maxEnemies = Mathf.Min(_config.maxEnemies, enemyPoints.Count);
        int enemyCount = Mathf.Min(_config.possibleEnemies.Length, maxEnemies);

        var shuffleSpawns = new List<SpawnPoint>(enemyPoints);
        Shuffle(shuffleSpawns);

        for (int i = 0; i < enemyCount; i++)
        {
            EnemyDefinitionSO data = _config.possibleEnemies[i];
            SpawnPoint spawn = shuffleSpawns[i];

            GameObject enemyObj = _enemyFactory.CreateEnemy(data, spawn.transform.position);
        }
    }

    /////////////////////////////////////////////////////////////////////////////

    // UTILS

    private void Shuffle(List<SpawnPoint> points)
    {
        for (int i = 0; i < points.Count; i++)
        {
            int rand = Random.Range(i, points.Count);
            (points[i], points[rand]) = (points[rand], points[i]);
        }
    }
}
