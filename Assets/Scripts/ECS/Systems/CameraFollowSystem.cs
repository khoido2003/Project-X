using UnityEngine;

public class CameraFollowSystem : ISystem
{
    private ICameraService _cameraService;
    private World _world;

    public void Initialize(World world)
    {
        _world = world;

        if (!world.Services.TryResolve<ICameraService>(out _cameraService))
        {
            Debug.LogError("CameraFollowSystem: No ICameraService registered!");

            return;
        }

        world.Events.Subscribe<PlayerSpawnEvent>(OnPlayerSpawned);
    }

    public void Shutdown()
    {
        _world?.Events.Unsubscribe<PlayerSpawnEvent>(OnPlayerSpawned);
    }

    public void Update(float dt) { }

    public void FixedUpdate(float dt) { }

    private void OnPlayerSpawned(PlayerSpawnEvent @event)
    {
        if (_cameraService == null)
        {
            return;
        }

        _cameraService.Follow(@event.Transform);
    }
}
