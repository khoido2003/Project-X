using System;
using System.Collections.Concurrent;
using UnityEngine;

public class UnityMainThreadDispatcher : MonoBehaviour
{
    private static UnityMainThreadDispatcher _instance;
    private readonly ConcurrentQueue<Action> _queue = new();

    public static UnityMainThreadDispatcher Instance()
    {
        if (_instance == null)
        {
            var go = new GameObject("UnityMainThreadDispatcher");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<UnityMainThreadDispatcher>();
        }
        return _instance;
    }

    public void Enqueue(Action action)
    {
        if (action == null)
        {
            return;
        }
        _queue.Enqueue(action);
    }

    private void Update()
    {
        while (_queue.TryDequeue(out var a))
        {
            try
            {
                a();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }
    }
}
