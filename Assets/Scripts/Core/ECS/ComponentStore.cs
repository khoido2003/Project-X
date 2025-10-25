using System;
using System.Collections;
using System.Collections.Generic;

public class ComponentStore
{
    private const int DICTIONARY_CAPACITY = 64;
    private readonly Dictionary<Type, IDictionary<EntityId, object>> _storage = new();

    public event Action<EntityId, Type> OnComponentAdded;

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

        OnComponentAdded?.Invoke(entity, type);
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
        {
            return component;
        }

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

    // 1 component
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

    // 2 components
    public IEnumerable<(EntityId, T1, T2)> Query<T1, T2>()
        where T1 : class
        where T2 : class
    {
        if (!_storage.TryGetValue(typeof(T1), out var dict1))
        {
            yield break;
        }

        if (!_storage.TryGetValue(typeof(T2), out var dict2))
        {
            yield break;
        }

        foreach (var kvp in dict1)
        {
            if (dict2.TryGetValue(kvp.Key, out var obj2))
            {
                yield return (kvp.Key, (T1)kvp.Value, (T2)obj2);
            }
        }
    }

    // 3 components
    public IEnumerable<(EntityId, T1, T2, T3)> Query<T1, T2, T3>()
        where T1 : class
        where T2 : class
        where T3 : class
    {
        if (!_storage.TryGetValue(typeof(T1), out var dict1))
        {
            yield break;
        }
        if (!_storage.TryGetValue(typeof(T2), out var dict2))
        {
            yield break;
        }

        if (!_storage.TryGetValue(typeof(T3), out var dict3))
        {
            yield break;
        }

        foreach (var kvp in dict1)
        {
            var id = kvp.Key;
            if (dict2.TryGetValue(id, out var obj2) && dict3.TryGetValue(id, out var obj3))
            {
                yield return (id, (T1)kvp.Value, (T2)obj2, (T3)obj3);
            }
        }
    }

    // 4 components
    public IEnumerable<(EntityId, T1, T2, T3, T4)> Query<T1, T2, T3, T4>()
        where T1 : class
        where T2 : class
        where T3 : class
        where T4 : class
    {
        if (!_storage.TryGetValue(typeof(T1), out var dict1))
        {
            yield break;
        }

        if (!_storage.TryGetValue(typeof(T2), out var dict2))
        {
            yield break;
        }
        if (!_storage.TryGetValue(typeof(T3), out var dict3))
        {
            yield break;
        }
        if (!_storage.TryGetValue(typeof(T4), out var dict4))
        {
            yield break;
        }

        foreach (var kvp in dict1)
        {
            var id = kvp.Key;

            if (
                dict2.TryGetValue(id, out var obj2)
                && dict3.TryGetValue(id, out var obj3)
                && dict4.TryGetValue(id, out var obj4)
            )
            {
                yield return (id, (T1)kvp.Value, (T2)obj2, (T3)obj3, (T4)obj4);
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
