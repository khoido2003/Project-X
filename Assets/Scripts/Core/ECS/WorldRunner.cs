using UnityEngine;

[DefaultExecutionOrder(-90)]
public class WorldRunner : MonoBehaviour
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

    public World World { get; private set; }

    public static WorldRunner Instance { get; private set; }

    private SpawnSystem _spawnSystem;

    private void Awake()
    {
        Instance = this;
        World = new World();
        InitServices();

        // Client only
        InitOfflineSystems();
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

    private void OnDestroy()
    {
        World.Systems.ShutdownAll();
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

    private void InitOfflineSystems()
    {
        World.Systems.AddSystem(new SpawnSystem(spawnConfig), World);

        World.Systems.AddSystem(new InputSystem(), World);
        World.Systems.AddSystem(new CameraFollowSystem(), World);
        World.Systems.AddSystem(new TransformSyncSystem(), World);

        World.Systems.AddSystem(new HealthSystem(), World);
        World.Systems.AddSystem(new MovementSystem(), World);
        World.Systems.AddSystem(new AttackSystem(), World);
        World.Systems.AddSystem(new DamageSystem(), World);
        World.Systems.AddSystem(new SkillSystem(), World);
        World.Systems.AddSystem(new CombatStateSystem(), World);

        World.Systems.AddSystem(new EnemyVisionSystem(), World);
        World.Systems.AddSystem(new EnemyPathfindingSystem(), World);
        World.Systems.AddSystem(new EnemyMovementSystem(), World);
        World.Systems.AddSystem(new EnemyAISystem(), World);

        EnemyAIHelpers.RegisterDefaultStates();
    }
}
