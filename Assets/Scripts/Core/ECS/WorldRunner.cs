using UnityEngine;

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

    private CharacterSpawnSystem _spawnSystem;

    private void Awake()
    {
        World = new World();

        Instance = this;

        InitServices();
        InitSystems();
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
        World.Services.Register<EntityViewRegistry>(entityViewRegistry);
    }

    private void InitSystems()
    {
        World.Systems.AddSystem(new InputSystem(), World);
        World.Systems.AddSystem(new CharacterSpawnSystem(spawnConfig), World);
        World.Systems.AddSystem(new TransformSyncSystem(), World);
        World.Systems.AddSystem(new MovementSystem(), World);
        World.Systems.AddSystem(new CameraFollowSystem(), World);
        World.Systems.AddSystem(new AnimationSyncSystem(), World);
        World.Systems.AddSystem(new AttackSystem(), World);
        World.Systems.AddSystem(new DamageSystem(), World);
        World.Systems.AddSystem(new SkillSystem(), World);
        World.Systems.AddSystem(new CombatStateSystem(), World);
    }
}
