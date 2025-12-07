using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[DefaultExecutionOrder(-90)]
public class WorldRunner : NetworkBehaviour
{
    [Header("Game Config")]
    [SerializeField]
    private SpawnConfigSO spawnConfig;

    [SerializeField]
    private EntityViewRegistry entityViewRegistry;

    [SerializeField]
    private InputService inputService;

    [SerializeField]
    private CinemachineCameraService cameraService;

    [SerializeField]
    private CharacterDefinitionSO[] characterData;

    [Header("Spawned Points")]
    [SerializeField]
    private Transform[] playerSpawnPoints = new Transform[4];

    private bool[] _spawnPointsUsed = new bool[4];

    public World World { get; private set; }

    public static WorldRunner Instance { get; private set; }

    private SpawnSystem _spawnSystem;

    //////////////////////////////////////////////////////////////////

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        World = new World();

        InitServices();
        InitSystems();

        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;

            StartCoroutine(DelayedSpawnExistingPlayers());
        }
    }

    private void Update()
    {
        var time = World.Services.Resolve<ITimeService>();
        World.Systems.UpdateAll(time.DeltaTime);
    }

    private void FixedUpdate()
    {
        var time = World.Services.Resolve<ITimeService>();
        World.Systems.FixedUpdateAll(time.FixedDeltaTime);
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();

        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        }

        World?.Systems.ShutdownAll();
    }

    private void OnDestroy()
    {
        World.Systems.ShutdownAll();

        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        if (!IsServer)
        {
            return;
        }

        if (HasSpawnedPlayer(clientId))
        {
            Debug.Log($"Client {clientId} already has a spawned player, skipping spawn!");

            return;
        }

        SpawnPlayerForClient(clientId);
    }

    /////////////////////////////////////////////////////////////////////////

    private System.Collections.IEnumerator DelayedSpawnExistingPlayers()
    {
        // Wait one frame to ensure everything is initialized
        yield return null;

        SpawnExistingPlayers();
    }

    private void SpawnExistingPlayers()
    {
        if (!IsServer)
        {
            return;
        }

        Debug.Log(
            $"Spawningng existing players. Connected Clients: {NetworkManager.Singleton.ConnectedClientsIds.Count}"
        );

        foreach (var clientId in NetworkManager.Singleton.ConnectedClientsIds)
        {
            SpawnPlayerForClient(clientId);
        }
    }

    private void SpawnPlayerForClient(ulong clientId)
    {
        CharacterDefinitionSO characterData = GetCharacterForClient(clientId);

        if (characterData == null)
        {
            Debug.LogError($"No character data found for client  {clientId}");
            return;
        }

        Vector3 spawnPosition = GetAvailableSpawnPoint();

        _spawnSystem.SpawnNetworkPlayer(clientId, characterData, spawnPosition);
    }

    private bool HasSpawnedPlayer(ulong clientId)
    {
        foreach (var (_, owner) in World.Components.Query<NetworkOwnerComponent>())
        {
            if (owner.ClientId == clientId)
            {
                return true;
            }
        }

        return false;
    }

    private CharacterDefinitionSO GetCharacterForClient(ulong clientId)
    {
        foreach (var character in characterData)
        {
            if (character.isSelected && character.clientId == clientId)
            {
                Debug.Log(character.name);
                return character;
            }
        }

        Debug.LogError($"No character selected for client {clientId}");

        return null;
    }

    private Vector3 GetAvailableSpawnPoint()
    {
        if (playerSpawnPoints == null || playerSpawnPoints.Length == 0)
        {
            Debug.LogWarning("No spawn points assigned! Using default position");
            return Vector3.zero;
        }

        var availableIndices = new List<int>();

        for (int i = 0; i < playerSpawnPoints.Length && i < 4; i++)
        {
            if (!_spawnPointsUsed[i] && playerSpawnPoints[i] != null)
            {
                availableIndices.Add(i);
            }
        }

        if (availableIndices.Count == 0)
        {
            Debug.LogWarning("All spawn points used! Reusing a random one");

            for (int i = 0; i < 4; i++)
            {
                _spawnPointsUsed[i] = false;
            }

            availableIndices.Add(UnityEngine.Random.Range(0, Mathf.Min(playerSpawnPoints.Length, 4)));
        }

        int randomIndex = availableIndices[UnityEngine.Random.Range(0, availableIndices.Count)];

        _spawnPointsUsed[randomIndex] = true;

        return playerSpawnPoints[randomIndex].position;
    }

    public void FreeSpawnPoint(Vector3 position)
    {
        for (int i = 0; i < playerSpawnPoints.Length && i < 4; i++)
        {
            if (playerSpawnPoints[i] != null && Vector3.Distance(playerSpawnPoints[i].position, position) < 0.1f)
            {
                _spawnPointsUsed[i] = false;
                break;
            }
        }
    }

    private void InitServices()
    {
        // Time Service
        World.Services.Register<ITimeService>(new UnityTimeService());

        // Camera Service
        if (cameraService == null)
        {
            Debug.LogError("No CinemachineCamera found");
            return;
        }
        World.Services.Register<ICameraService>(cameraService);

        // InputService
        if (inputService == null)
        {
            Debug.LogError("No InputService found!");
            return;
        }

        World.Services.Register<IInputService>(inputService);

        // EntityView Registry
        if (entityViewRegistry == null)
        {
            Debug.LogError("No EntityViewRegistry found!");
            return;
        }
        World.Services.Register(entityViewRegistry);

        // Object pool
        var poolService = new ObjectPoolService();
        World.Services.Register(poolService);
    }

    private void InitSystems()
    {
        _spawnSystem = new SpawnSystem(spawnConfig);
        World.Systems.AddSystem(_spawnSystem, World);

        World.Systems.AddSystem(new InputSystem(), World);
        World.Systems.AddSystem(new CameraFollowSystem(), World);
        World.Systems.AddSystem(new TransformSyncSystem(), World);

        World.Systems.AddSystem(new HealthSystem(), World);
        World.Systems.AddSystem(new MovementSystem(), World);
        World.Systems.AddSystem(new AttackSystem(), World);
        World.Systems.AddSystem(new DamageSystem(), World);
        World.Systems.AddSystem(new SkillSystem(), World);
        World.Systems.AddSystem(new CombatStateSystem(), World);

        World.Systems.AddSystem(new StunSystem(), World);
        World.Systems.AddSystem(new KnockbackSystem(), World);
        World.Systems.AddSystem(new HealthRegenSystem(), World);
        World.Systems.AddSystem(new PlayerRespawnSystem(), World);

        World.Systems.AddSystem(new EnemyVisionSystem(), World);
        World.Systems.AddSystem(new EnemyPathfindingSystem(), World);
        World.Systems.AddSystem(new EnemyMovementSystem(), World);
        World.Systems.AddSystem(new EnemyAISystem(), World);

        EnemyAIHelpers.RegisterDefaultStates();
    }
}
