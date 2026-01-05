using System.Collections.Generic;

public class EntityManager
{
    private int _nextId = 1;
    private int _nextTempId = 10000;
    private readonly HashSet<EntityId> _live = new();

    // NOTE: Removed ID recycling (_freeIds queue) to prevent stale ID bugs.
    // When entities are destroyed and IDs are reused, stale references in caches,
    // event handlers, and other systems can cause subtle bugs.
    // For Unity games, you'll never hit 2 billion entities per match.

    public EntityId CreateEntity()
    {
        int idValue = _nextId++;
        var id = new EntityId(idValue);
        _live.Add(id);
        return id;
    }

    /// <summary>
    /// Creates a temporary entity with an ID in the high range (10000+).
    /// Use for projectiles, drones, and other short-lived gameplay objects.
    /// </summary>
    public EntityId CreateTemporaryEntity()
    {
        int idValue = _nextTempId++;
        var id = new EntityId(idValue);
        _live.Add(id);
        return id;
    }

    public bool Exists(EntityId id) => _live.Contains(id);

    public bool DestroyEntity(EntityId id)
    {
        // Just remove from live set - don't recycle the ID
        return _live.Remove(id);
    }

    public IReadOnlyCollection<EntityId> GetAllEntities() => _live;

    public void Reset()
    {
        _live.Clear();
        _nextId = 1;
        _nextTempId = 10000;
    }
}
