using UnityEngine;

public class CameraFollowSystem : ISystem
{
    private ICameraService _cameraService;
    private World _world;
    private bool _hasSetCamera;
    private float _retryTimer;
    private const float RETRY_INTERVAL = 0.5f;

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

    public void Update(float dt)
    {
        if (!_hasSetCamera)
        {
            _retryTimer += dt;

            if (_retryTimer >= RETRY_INTERVAL)
            {
                _retryTimer = 0f;
                TrySetCameraToLocalPlayer();
            }
        }
    }

    public void FixedUpdate(float dt) { }

    private void OnPlayerSpawned(PlayerSpawnEvent @event)
    {
        if (_cameraService == null)
        {
            Debug.LogWarning("[CameraFollowSystem] CameraService is null!");
            return;
        }

        if (_hasSetCamera)
        {
            return;
        }

        if (_world.Components.TryGet(@event.Entity, out NetworkOwnerComponent owner))
        {
            if (owner.IsLocalPlayer)
            {
                _cameraService.Follow(@event.Transform);
                _hasSetCamera = true;
            }
        }
        else
        {
            Debug.LogWarning($"[CameraFollowSystem] Entity {@event.Entity.Id} has no NetworkOwnerComponent!");
        }
    }

    private void TrySetCameraToLocalPlayer()
    {
        foreach (var (entity, owner, _) in _world.Components.Query<NetworkOwnerComponent, PlayerTagComponent>())
        {
            if (owner.IsLocalPlayer)
            {
                var registry = _world.Services.Resolve<EntityViewRegistry>();

                if (registry.TryGet(entity, out EntityView view))
                {
                    _cameraService.Follow(view.transform);
                    _hasSetCamera = true;
                    return;
                }
            }
        }
    }
}
