using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Handles spawning spectator camera rig for spectator clients.
/// Attach this to a GameObject in the Game scene.
///
/// When a spectator client joins:
/// - Does NOT spawn a player entity
/// - Instantiates a local-only SpectatorCameraRig
/// - Disables player-specific systems/UI
/// </summary>
public class SpectatorSpawner : MonoBehaviour
{
    [Header("Spectator Prefab")]
    [SerializeField]
    private GameObject _spectatorCameraRigPrefab;

    [Header("Spawn Settings")]
    [SerializeField]
    private Vector3 _defaultSpawnPosition = new Vector3(0f, 20f, 10f);

    [SerializeField]
    private Vector3 _defaultLookDirection = new Vector3(60f, 180f, 0f);

    private GameObject _spectatorInstance;
    private bool _isSpectator = false;

    public static SpectatorSpawner Instance { get; private set; }

    /// <summary>
    /// Returns true if the local client is a spectator (not a player).
    /// </summary>
    public bool IsLocalSpectator => _isSpectator;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        StartCoroutine(InitializeSpectator());
    }

    private IEnumerator InitializeSpectator()
    {
        // Wait for network to be ready
        while (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsClient)
        {
            yield return null;
        }

        // Small delay to ensure connection is stable
        yield return new WaitForSeconds(0.5f);

        // Check if this client is a spectator
        if (ConnectionSettings.IsSpectator)
        {
            _isSpectator = true;
            SpawnSpectatorCamera();

            Debug.Log("[SpectatorSpawner] Spectator client detected - camera rig spawned");
        }
        else
        {
            Debug.Log("[SpectatorSpawner] Player client detected - normal spawn flow");
        }

        // Reset the flag after use (to prevent issues if player returns to menu and rejoins)
        // Note: Don't reset here if you want the flag to persist for the session
    }

    private void SpawnSpectatorCamera()
    {
        if (_spectatorCameraRigPrefab == null)
        {
            Debug.LogError("[SpectatorSpawner] No spectator camera rig prefab assigned! Creating fallback camera.");
            CreateFallbackCamera();
            return;
        }

        // Instantiate locally (not networked)
        _spectatorInstance = Instantiate(
            _spectatorCameraRigPrefab,
            _defaultSpawnPosition,
            Quaternion.Euler(_defaultLookDirection)
        );

        // Initialize controller if present
        var controller = _spectatorInstance.GetComponent<SpectatorController>();
        if (controller != null)
        {
            controller.SetInitialPosition(_defaultSpawnPosition, Quaternion.Euler(_defaultLookDirection));
        }

        // Disable any main cameras that might conflict
        DisableOtherCameras();
        
        // Configure spectator camera properly
        ConfigureSpectatorCamera();
    }
    
    /// <summary>
    /// Configure the spectator camera to render all layers and handle audio properly.
    /// </summary>
    private void ConfigureSpectatorCamera()
    {
        Camera spectatorCam = _spectatorInstance.GetComponentInChildren<Camera>();
        if (spectatorCam != null)
        {
            // Render ALL layers so VFX, particles, etc. are visible
            spectatorCam.cullingMask = -1; // -1 = Everything
            
            // Ensure proper depth and clear flags
            spectatorCam.depth = 10; // Higher depth to ensure it renders on top
            spectatorCam.clearFlags = CameraClearFlags.Skybox;
            
            Debug.Log($"[SpectatorSpawner] Configured spectator camera - cullingMask: Everything");
        }
        
        // Handle AudioListener - disable any extras to prevent warning
        AudioListener[] listeners = _spectatorInstance.GetComponentsInChildren<AudioListener>();
        if (listeners.Length > 0)
        {
            // Keep only the first one enabled
            for (int i = 1; i < listeners.Length; i++)
            {
                listeners[i].enabled = false;
            }
        }
    }

    private void CreateFallbackCamera()
    {
        // Create a basic spectator camera as fallback
        GameObject cameraObj = new GameObject("SpectatorCamera_Fallback");
        cameraObj.transform.position = _defaultSpawnPosition;
        cameraObj.transform.rotation = Quaternion.Euler(_defaultLookDirection);

        Camera cam = cameraObj.AddComponent<Camera>();
        cam.tag = "MainCamera";
        cam.cullingMask = -1; // Render everything

        cameraObj.AddComponent<SpectatorController>();

        _spectatorInstance = cameraObj;

        DisableOtherCameras();
    }

    private void DisableOtherCameras()
    {
        // Disable other cameras in the scene
        Camera[] allCameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
        Camera spectatorCam = _spectatorInstance?.GetComponentInChildren<Camera>();

        foreach (Camera cam in allCameras)
        {
            if (cam != spectatorCam && cam.CompareTag("MainCamera"))
            {
                cam.enabled = false;
                
                // Also disable AudioListener on disabled cameras to prevent warning
                AudioListener listener = cam.GetComponent<AudioListener>();
                if (listener != null)
                {
                    listener.enabled = false;
                }
                
                Debug.Log($"[SpectatorSpawner] Disabled camera: {cam.gameObject.name}");
            }
        }
        
        // Disable ALL other AudioListeners in the scene too
        AudioListener[] allListeners = FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
        AudioListener spectatorListener = _spectatorInstance?.GetComponentInChildren<AudioListener>();
        
        foreach (AudioListener listener in allListeners)
        {
            if (listener != spectatorListener)
            {
                listener.enabled = false;
            }
        }
    }

    private void OnDestroy()
    {
        if (_spectatorInstance != null)
        {
            Destroy(_spectatorInstance);
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }
}
