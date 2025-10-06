using UnityEngine;

public class WorldRunner : MonoBehaviour
{
    [Header("Game Config")]
    [SerializeField]
    private SpawnConfigSO spawnConfig;

    public World World { get; private set; }

    private SpawnSystem _spawnSystem;

    private void Awake()
    {
        World = new World();

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
        World.Services.Register<ITimeService>(new UnityTimeService());

        var cameraService = FindAnyObjectByType<CinemachineCameraService>();
        if (cameraService == null)
        {
            Debug.LogError("No CinemachineCamera found");
        }
        if (cameraService != null)
        {
            World.Services.Register<ICameraService>(cameraService);
        }
    }

    private void InitSystems()
    {
        World.Systems.AddSystem(new SpawnSystem(spawnConfig), World);
        World.Systems.AddSystem(new MovementSystem(), World);
        World.Systems.AddSystem(new AnimationSystem(), World);
        World.Systems.AddSystem(new CameraFollowSystem(), World);
    }
}
