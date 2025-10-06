using System.Collections.Generic;

public class EntityManager
{
    private int _nextId = 1;
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

    public bool Exists(EntityId id) => _live.Contains(id);

    public bool DestroyEntity(EntityId id)
    {
        if (_live.Remove(id))
        {
            _freeIds.Enqueue(id.Id);
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
    }
}
