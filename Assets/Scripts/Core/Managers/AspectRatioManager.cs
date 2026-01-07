using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Manages CanvasScaler reference resolutions based on the selected aspect ratio.
/// Attach this to a persistent GameObject (e.g., the SettingsManager).
/// </summary>
public class AspectRatioManager : MonoBehaviour
{
    public static AspectRatioManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnEnable()
    {
        SettingsManager.OnAspectRatioChanged += OnAspectRatioChanged;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SettingsManager.OnAspectRatioChanged -= OnAspectRatioChanged;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Start()
    {
        // Apply current aspect ratio to all canvases in the scene
        ApplyCurrentAspectRatio();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Apply aspect ratio to newly loaded scene
        ApplyCurrentAspectRatio();
    }

    private void OnAspectRatioChanged(AspectRatioMode mode)
    {
        ApplyAspectRatioToAllCanvases(mode);
    }

    /// <summary>
    /// Applies the current aspect ratio setting to all CanvasScalers.
    /// </summary>
    public void ApplyCurrentAspectRatio()
    {
        if (SettingsManager.Instance == null) return;

        int aspectRatioIndex = SettingsManager.Instance.GetAspectRatio();
        AspectRatioMode mode = (AspectRatioMode)aspectRatioIndex;
        ApplyAspectRatioToAllCanvases(mode);
    }

    /// <summary>
    /// Applies the specified aspect ratio to all CanvasScalers in the scene.
    /// </summary>
    private void ApplyAspectRatioToAllCanvases(AspectRatioMode mode)
    {
        Vector2 referenceResolution = SettingsManager.GetReferenceResolution(mode);

        // Find all CanvasScalers in the scene (including inactive)
        CanvasScaler[] canvasScalers = Resources.FindObjectsOfTypeAll<CanvasScaler>();

        foreach (CanvasScaler scaler in canvasScalers)
        {
            // Skip if not in a valid scene (prefabs, etc.)
            if (scaler.gameObject.scene.name == null || !scaler.gameObject.scene.isLoaded)
                continue;

            // Only adjust screen space canvases with ScaleWithScreenSize mode
            if (scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize)
            {
                scaler.referenceResolution = referenceResolution;
            }
        }

        Debug.Log($"[AspectRatioManager] Applied {mode} ({referenceResolution.x}x{referenceResolution.y}) to all CanvasScalers");
    }
}
