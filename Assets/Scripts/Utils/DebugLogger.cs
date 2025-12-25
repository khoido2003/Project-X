using System.Diagnostics;
using UnityEngine;

/// <summary>
/// Performance-optimized debug logging.
/// All Log methods are stripped from non-editor builds via Conditional attribute.
/// Use this instead of Debug.Log/Debug.LogWarning/Debug.LogError for performance.
/// </summary>
public static class DebugLogger
{
    /// <summary>
    /// Log a message. Only compiled in UNITY_EDITOR.
    /// </summary>
    [Conditional("UNITY_EDITOR")]
    public static void Log(string message)
    {
        UnityEngine.Debug.Log(message);
    }

    /// <summary>
    /// Log a formatted message. Only compiled in UNITY_EDITOR.
    /// </summary>
    [Conditional("UNITY_EDITOR")]
    public static void Log(string message, Object context)
    {
        UnityEngine.Debug.Log(message, context);
    }

    /// <summary>
    /// Log a warning. Only compiled in UNITY_EDITOR.
    /// </summary>
    [Conditional("UNITY_EDITOR")]
    public static void LogWarning(string message)
    {
        UnityEngine.Debug.LogWarning(message);
    }

    /// <summary>
    /// Log a warning with context. Only compiled in UNITY_EDITOR.
    /// </summary>
    [Conditional("UNITY_EDITOR")]
    public static void LogWarning(string message, Object context)
    {
        UnityEngine.Debug.LogWarning(message, context);
    }

    /// <summary>
    /// Log an error. Always compiled (errors should always be visible).
    /// </summary>
    public static void LogError(string message)
    {
        UnityEngine.Debug.LogError(message);
    }

    /// <summary>
    /// Log an error with context. Always compiled (errors should always be visible).
    /// </summary>
    public static void LogError(string message, Object context)
    {
        UnityEngine.Debug.LogError(message, context);
    }
}
