using System;
using System.Collections.Generic;

public class ComponentStore
{
    private const int DICTIONARY_CAPACITY = 64;
    private readonly Dictionary<Type, IDictionary<EntityId, object>> _storage = new();

    public void Add<T>(EntityId entity, T component)
        where T : class
    {
        Type type = typeof(T);

        if (!_storage.TryGetValue(type, out var dict))
        {
            dict = new Dictionary<EntityId, object>(DICTIONARY_CAPACITY);
            _storage[type] = dict;
        }

        dict[entity] =
            component
            ?? throw new ArgumentNullException(
                nameof(component),
                $"Cannot add null component of type {typeof(T).Name}."
            );
    }

    public bool TryGet<T>(EntityId entity, out T component)
        where T : class
    {
        component = null;

        if (_storage.TryGetValue(typeof(T), out var dict) && dict.TryGetValue(entity, out var obj))
        {
            component = obj as T;
            return component != null;
        }

        return false;
    }

    public T Get<T>(EntityId entity)
        where T : class
    {
        if (TryGet(entity, out T component))
            return component;

        throw new KeyNotFoundException($"Component {typeof(T).Name} for entity {entity} not found.");
    }

    public bool Has<T>(EntityId entity)
        where T : class
    {
        return _storage.TryGetValue(typeof(T), out var dict) && dict.ContainsKey(entity);
    }

    public bool Remove<T>(EntityId entity)
        where T : class
    {
        if (_storage.TryGetValue(typeof(T), out var dict))
        {
            return dict.Remove(entity);
        }

        return false;
    }

    public IEnumerable<KeyValuePair<EntityId, T>> Query<T>()
        where T : class
    {
        if (_storage.TryGetValue(typeof(T), out var dict))
        {
            foreach (var kvp in dict)
            {
                yield return new KeyValuePair<EntityId, T>(kvp.Key, (T)kvp.Value);
            }
        }
    }

    public void RemoveAllComponents(EntityId entity)
    {
        foreach (var dict in _storage.Values)
        {
            dict.Remove(entity);
        }
    }
}
