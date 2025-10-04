using System.Collections.Generic;

public class EntityManager
{
    private int _nextId = 1;
    private readonly HashSet<EntityId> _live = new HashSet<EntityId>();

    public EntityId CreateEntity()
    {
        var id = new EntityId(_nextId++);
        _live.Add(id);
        return id;
    }

    public bool Exists(EntityId id) => _live.Contains(id);

    internal bool DestroyEntity(EntityId id)
    {
        return _live.Remove(id);
    }

    public IReadOnlyCollection<EntityId> GetAllEntities() => _live;
}
