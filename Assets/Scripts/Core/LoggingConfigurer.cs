using UnityEngine;

/// <summary>
/// Disables Unity debug logging in non-development builds for performance.
/// Attach this to a GameObject that exists before any logging occurs (e.g., Bootstrap scene).
/// </summary>
[DefaultExecutionOrder(-1000)]
public class LoggingConfigurer : MonoBehaviour
{
    [Header("Logging Configuration")]
    [Tooltip("If true, disables all Debug.Log output in builds (not editor)")]
    [SerializeField] private bool disableLoggingInBuilds = true;
    
    [Tooltip("If true, also disables logging in Development Builds")]
    [SerializeField] private bool disableInDevelopmentBuilds = true;

    private void Awake()
    {
        // Always enable in Editor for development
        #if UNITY_EDITOR
        Debug.unityLogger.logEnabled = true;
        return;
        #endif
        
        if (!disableLoggingInBuilds)
            return;
            
        // Check if this is a development build
        #if DEVELOPMENT_BUILD
        if (!disableInDevelopmentBuilds)
            return;
        #endif
        
        // Disable all logging for maximum performance
        Debug.unityLogger.logEnabled = false;
        
        // Alternatively, disable only Log and Warning but keep Errors:
        // Debug.unityLogger.filterLogType = LogType.Error;
    }
}
