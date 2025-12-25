using System;
using System.Collections.Generic;
using UnityEngine;

public class EventBus
{
    private readonly Dictionary<Type, List<Delegate>> _subscribers = new();
    private readonly Dictionary<Type, object> _lastEvents = new();
    
    // Reusable buffer to avoid allocation in Publish (grows as needed)
    private Delegate[] _publishBuffer = new Delegate[32];

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
            
        // Grow buffer if needed (rare, only happens once per size threshold)
        if (_publishBuffer.Length < count)
        {
            _publishBuffer = new Delegate[count * 2];
        }
        
        // Copy to buffer to avoid mutation issues while iterating
        list.CopyTo(_publishBuffer);
        
        // CRITICAL: Clear unused buffer slots to prevent stale delegates from previous larger lists
        // causing InvalidCastException when trying to cast them as Action<T>
        for (int i = count; i < _publishBuffer.Length && _publishBuffer[i] != null; i++)
        {
            _publishBuffer[i] = null;
        }

        for (int i = 0; i < count; i++)
        {
            try
            {
                // Additional type safety check to prevent InvalidCastException
                if (_publishBuffer[i] is Action<T> handler)
                {
                    handler.Invoke(evt);
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogException(ex);
            }
        }
    }
}
