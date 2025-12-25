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
    private AudioService audioService;

    [Header("Audio Configuration")]
    [SerializeField]
    private SceneAudioConfig sceneAudioConfig;

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
        // PERFORMANCE: Disable debug logging in builds (not editor)
        // This prevents massive performance overhead from Debug.Log calls
        #if !UNITY_EDITOR
        Debug.unityLogger.logEnabled = false;
        #endif
        
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        World = new World();
        InitServices();
        InitSystems();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // Play game music when game starts
        PlayGameMusic();

        // Register server-only systems HERE where IsServer is valid
        // (not in Awake where network hasn't started yet)
        if (IsServer)
        {
            InitServerSystems();
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            StartCoroutine(DelayedSpawnExistingPlayers());
        }
    }

    private void PlayGameMusic()
    {
        if (sceneAudioConfig == null || audioService == null)
        {
            return;
        }

        // Get current scene name
        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (System.Enum.TryParse<SceneName>(sceneName, out SceneName sceneEnum))
        {
            AudioClip music = sceneAudioConfig.GetMusicForScene(sceneEnum);
            if (music != null)
            {
                AudioHelper.PlayMusic(World, music, sceneAudioConfig.musicFadeInTime);
            }
        }
    }

    private void Update()
    {
        // All clients run Update
        var time = World.Services.Resolve<ITimeService>();
        World.Systems.UpdateAll(time.DeltaTime);
    }

    private void FixedUpdate()
    {
        // All clients run FixedUpdate
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
        World?.Systems.ShutdownAll();

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
            Debug.LogError($"No character data found for client {clientId}");
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

        // Audio Service
        if (audioService == null)
        {
            audioService = AudioService.Instance;

            if (audioService == null)
            {
                audioService = FindFirstObjectByType<AudioService>();
            }

            if (audioService == null)
            {
                Debug.LogWarning("No AudioService found in scene - instantiating one.");
                var go = new GameObject("AudioService");
                audioService = go.AddComponent<AudioService>();
            }
        }
        else if (AudioService.Instance != null && AudioService.Instance != audioService)
        {
            Debug.LogWarning("AudioService reference set but singleton already exists; using singleton.");
            audioService = AudioService.Instance;
        }

        if (audioService != null)
        {
            World.Services.Register<IAudioService>(audioService);
        }
    }

    private void InitSystems()
    {
        // ===== SYSTEMS FOR ALL CLIENTS (Visual/Audio/Camera/Input) =====
        World.Systems.AddSystem(new CameraFollowSystem(), World);
        World.Systems.AddSystem(new TransformSyncSystem(), World);
        World.Systems.AddSystem(new AudioSystem(), World);
        World.Systems.AddSystem(new AudioProfileSystem(), World);
        
        // InputSystem must run on ALL clients to handle local input
        // It sends RPCs to server (RequestAttackServerRpc, skill casts, etc.)
        World.Systems.AddSystem(new InputSystem(), World);
        
        // SkillSystem must run on ALL clients for skill preview and local feedback
        // Actual skill execution is validated by server, but preview runs locally
        World.Systems.AddSystem(new SkillSystem(), World);
        
        // NOTE: Server-only systems are registered in InitServerSystems() called from OnNetworkSpawn()
        // because IsServer is only valid after network starts
    }

    private void InitServerSystems()
    {
        // Spawning
        _spawnSystem = new SpawnSystem(spawnConfig);
        World.Systems.AddSystem(_spawnSystem, World);
        
        // Core gameplay (SkillSystem is initialized in InitSystems for all clients)
        World.Systems.AddSystem(new MovementSystem(), World);
        World.Systems.AddSystem(new HealthSystem(), World);
        World.Systems.AddSystem(new AttackSystem(), World);
        World.Systems.AddSystem(new DamageSystem(), World);
        World.Systems.AddSystem(new CombatStateSystem(), World);
        
        // Status effects
        World.Systems.AddSystem(new StunSystem(), World);
        World.Systems.AddSystem(new KnockbackSystem(), World);
        World.Systems.AddSystem(new HealthRegenSystem(), World);
        World.Systems.AddSystem(new PlayerRespawnSystem(), World);
        
        // Enemy AI
        World.Systems.AddSystem(new EnemyVisionSystem(), World);
        World.Systems.AddSystem(new EnemyPathfindingSystem(), World);
        World.Systems.AddSystem(new EnemyMovementSystem(), World);
        World.Systems.AddSystem(new EnemyAISystem(), World);
        
        EnemyAIHelpers.RegisterDefaultStates();
    }

}
