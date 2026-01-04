using System.Collections.Generic;

public class EntityManager
{
    private int _nextId = 1;
    private int _nextTempId = 10000;
    private readonly HashSet<EntityId> _live = new();
    private readonly Queue<int> _freeIds = new();

    public EntityId CreateEntity()
    {
        int idValue;
        if (_freeIds.Count > 0)
        {
            idValue = _freeIds.Dequeue();
        }
        else
        {
            idValue = _nextId++;
        }
        var id = new EntityId(idValue);
        _live.Add(id);
        return id;
    }

    /// <summary>
    /// Creates a temporary entity with an ID in the high range (10000+).
    /// These IDs are never recycled and won't conflict with regular entities.
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
        if (_live.Remove(id))
        {
            // Only recycle regular entity IDs (below 10000), not temporary ones
            if (id.Id < 10000)
            {
                _freeIds.Enqueue(id.Id);
            }
            return true;
        }
        return false;
    }

    public IReadOnlyCollection<EntityId> GetAllEntities() => _live;

    public void Reset()
    {
        _live.Clear();
        _freeIds.Clear();
        _nextId = 1;
        _nextTempId = 10000;
    }
}
