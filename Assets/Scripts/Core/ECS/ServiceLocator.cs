using System;
using System.Collections.Generic;

public interface IServiceLocator
{
    void Register<T>(T instance)
        where T : class;
    T Resolve<T>()
        where T : class;
    bool TryResolve<T>(out T instance)
        where T : class;
}

public class ServiceLocator : IServiceLocator
{
    private readonly Dictionary<Type, object> _services = new();

    public void Register<T>(T instance)
        where T : class
    {
        _services[typeof(T)] = instance;
    }

    public T Resolve<T>()
        where T : class
    {
        _services.TryGetValue(typeof(T), out object obj);
        return obj as T;
    }

    public bool TryResolve<T>(out T instance)
        where T : class
    {
        if (_services.TryGetValue(typeof(T), out object obj))
        {
            instance = obj as T;
            return instance != null;
        }
        instance = null;
        return false;
    }
}
