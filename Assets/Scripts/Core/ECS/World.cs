using System;

public readonly struct EntityDestroyedEvent
{
    public readonly EntityId Entity;

    public EntityDestroyedEvent(EntityId e) => Entity = e;
}

public class World
{
    private static World _instance;
    public static World Instance
    {
        get
        {
            if (_instance == null)
            {
                throw new InvalidOperationException("World has not been initialized yet!");
            }
            return _instance;
        }
    }

    public readonly ComponentStore Components = new();
    public readonly EventBus Events = new();
    public readonly ServiceLocator Services = new();
    public readonly EntityManager Entities = new();
    public readonly SystemManager Systems = new();

    public World()
    {
        _instance = this;
    }

    public EntityId CreateEntity() => Entities.CreateEntity();

    public void DestroyEntity(EntityId id)
    {
        // remove components
        Components.RemoveAllComponents(id);

        // remove entity
        Entities.DestroyEntity(id);

        // publish event
        Events.Publish(new EntityDestroyedEvent(id));
    }

    public static void Reset()
    {
        _instance = new World();
    }
}
