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
