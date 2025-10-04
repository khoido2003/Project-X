using UnityEngine;

public interface ITimeService
{
    float DeltaTime { get; }
    float FixedDeltaTime { get; }
}

public class UnityTimeService : ITimeService
{
    public float DeltaTime => Time.deltaTime;
    public float FixedDeltaTime => Time.fixedDeltaTime;
}

public class WorldRunner : MonoBehaviour
{
    public World World { get; private set; }

    private void Awake()
    {
        World = new World();

        World.Services.Register<ITimeService>(new UnityTimeService());

        // TODO register input/audio/network services here...
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
}
