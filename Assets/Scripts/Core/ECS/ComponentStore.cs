using System;
using System.Collections.Generic;

public class ComponentStore
{
    private readonly Dictionary<Type, object> _storage = new();

    public void Add<T>(EntityId entity, T component)
        where T : class
    {
        var t = typeof(T);
        if (!_storage.TryGetValue(t, out var raw))
        {
            var dict = new Dictionary<EntityId, T> { [entity] = component };
            _storage[t] = dict;
            return;
        }

        var typed = (Dictionary<EntityId, T>)raw;
        typed[entity] = component;
    }

    public bool TryGet<T>(EntityId entity, out T component)
        where T : class
    {
        component = null;
        if (_storage.TryGetValue(typeof(T), out object raw))
        {
            var typed = (Dictionary<EntityId, T>)raw;
            return typed.TryGetValue(entity, out component);
        }
        return false;
    }

    public T Get<T>(EntityId entity)
        where T : class
    {
        if (TryGet(entity, out T component))
        {
            return component;
        }
        throw new KeyNotFoundException($"Component {typeof(T).Name} for {entity} not found.");
    }

    public bool Has<T>(EntityId entity)
        where T : class
    {
        return TryGet<T>(entity, out _);
    }

    public bool Remove<T>(EntityId entity)
        where T : class
    {
        if (_storage.TryGetValue(typeof(T), out object raw))
        {
            var typed = (Dictionary<EntityId, T>)raw;
            return typed.Remove(entity);
        }
        return false;
    }

    public IEnumerable<KeyValuePair<EntityId, T>> Query<T>()
        where T : class
    {
        List<KeyValuePair<EntityId, T>> list = new();

        if (_storage.TryGetValue(typeof(T), out object raw))
        {
            var typed = (Dictionary<EntityId, T>)raw;

            foreach (var kv in typed)
            {
                list.Add(kv);
            }
        }
        return list;
    }

    public void RemoveAllComponents(EntityId entity)
    {
        var keys = new List<Type>(_storage.Keys);

        foreach (var type in keys)
        {
            object raw = _storage[type];
            var removeMethod = raw.GetType().GetMethod("Remove");
            removeMethod?.Invoke(raw, new object[] { entity });
        }
    }
}
