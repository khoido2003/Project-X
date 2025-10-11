using UnityEngine;

public class WorldRunner : MonoBehaviour
{
    [Header("Game Config")]
    [SerializeField]
    private SpawnConfigSO spawnConfig;

    public World World { get; private set; }

    private CharacterSpawnSystem _spawnSystem;

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
        World.Services.Register<ICameraService>(cameraService);
    }

    private void InitSystems()
    {
        World.Systems.AddSystem(new CharacterSpawnSystem(spawnConfig), World);
        World.Systems.AddSystem(new WeaponSpawnSystem(), World);
        World.Systems.AddSystem(new MovementSystem(), World);
        World.Systems.AddSystem(new CameraFollowSystem(), World);
        World.Systems.AddSystem(new AnimationSystem(), World);
        World.Systems.AddSystem(new AnimationSyncSystem(), World);
        World.Systems.AddSystem(new AttackSystem(), World);
    }
}
