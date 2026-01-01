using System;
using System.Collections.Generic;
using UnityEngine;

public class EventBus
{
    private readonly Dictionary<Type, List<Delegate>> _subscribers = new();
    private readonly Dictionary<Type, object> _lastEvents = new();

    public void Subscribe<T>(Action<T> handler)
    {
        Type t = typeof(T);

        if (!_subscribers.TryGetValue(t, out List<Delegate> list))
        {
            list = new List<Delegate>();
            _subscribers[t] = list;
        }

        list.Add(handler);

        // If there was a previously published event of this type, replay it immediately
        if (_lastEvents.TryGetValue(t, out object lastEvent))
        {
            try
            {
                handler((T)lastEvent);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }
    }

    public void Unsubscribe<T>(Action<T> handler)
    {
        var t = typeof(T);

        if (!_subscribers.TryGetValue(t, out var list))
        {
            return;
        }

        list.Remove(handler);

        if (list.Count == 0)
        {
            _subscribers.Remove(t);
        }
    }

    public void Publish<T>(T evt)
    {
        Type t = typeof(T);

        _lastEvents[t] = evt;

        if (!_subscribers.TryGetValue(t, out List<Delegate> list))
        {
            return;
        }

        int count = list.Count;
        if (count == 0)
            return;

        // Create a snapshot to avoid mutation issues while iterating
        var snapshot = list.ToArray();

        for (int i = 0; i < snapshot.Length; i++)
        {
            try
            {
                var del = snapshot[i];
                if (del == null)
                    continue;

                if (del is Action<T> handler)
                {
                    handler.Invoke(evt);
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }
    }
}
