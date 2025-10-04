using System.Collections.Generic;

public interface ISystem
{
    void Initialize(World world);
    void Update(float dt);
    void FixedUpdate(float dt);
    void Shutdown();
}

public class SystemManager
{
    private readonly List<ISystem> _systems = new();

    public void AddSystem(ISystem sys, World world)
    {
        _systems.Add(sys);
        sys.Initialize(world);
    }

    public void UpdateAll(float dt)
    {
        foreach (var s in _systems)
            s.Update(dt);
    }

    public void FixedUpdateAll(float dt)
    {
        foreach (var s in _systems)
            s.FixedUpdate(dt);
    }

    public void ShutdownAll()
    {
        foreach (var s in _systems)
            s.Shutdown();
        _systems.Clear();
    }
}
