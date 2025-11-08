using System;
using System.Collections.Concurrent;
using UnityEngine;

/// <summary>
/// Runs actions from other threads on the Unity main thread safely.
/// </summary>
public class UnityMainThreadDispatcher : MonoBehaviour
{
    private static readonly ConcurrentQueue<Action> queue = new();

    public static void Enqueue(Action action)
    {
        if (action == null)
        {
            return;
        }
        queue.Enqueue(action);
    }

    private void Update()
    {
        while (queue.TryDequeue(out var action))
        {
            action?.Invoke();
        }
    }
}
