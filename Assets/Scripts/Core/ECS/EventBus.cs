using System;
using System.Collections.Generic;

public class EventBus
{
    private readonly Dictionary<Type, List<Delegate>> _subscribers = new();

    public void Subscribe<T>(Action<T> handler)
    {
        Type t = typeof(T);

        if (!_subscribers.TryGetValue(t, out List<Delegate> list))
        {
            list = new List<Delegate>();
            _subscribers[t] = list;
        }

        list.Add(handler);
    }

    public void Unsubscribe<T>(Action<T> handler)
    {
        Type t = typeof(T);

        if (_subscribers.TryGetValue(t, out List<Delegate> list))
        {
            list.Remove(handler);

            if (list.Count == 0)
            {
                _subscribers.Remove(t);
            }
        }
    }

    public void Publish<T>(T evt)
    {
        Type t = typeof(T);

        if (!_subscribers.TryGetValue(t, out List<Delegate> list))
        {
            return;
        }

        // Copy to avoid mutation issues while iterating
        var subscribersCopy = list.ToArray();

        foreach (Delegate d in subscribersCopy)
        {
            try
            {
                ((Action<T>)d)?.Invoke(evt);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogException(ex);
            }
        }
    }
}
