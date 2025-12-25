using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Handles network heartbeat to detect dead connections.
/// Server sends periodic heartbeats, clients monitor for timeout.
/// </summary>
public class NetworkHeartbeatManager : NetworkBehaviour
{
    [Header("Heartbeat Settings")]
    [SerializeField] private float heartbeatInterval = 2f;     // How often server sends heartbeat
    [SerializeField] private float timeoutDuration = 10f;      // How long before client assumes disconnect
    [SerializeField] private float warningThreshold = 6f;      // When to show warning UI

    [Header("UI References")]
    [SerializeField] private GameObject connectionWarningUI;   // Optional UI to show when connection is bad

    private float _lastHeartbeatReceived;
    private float _heartbeatTimer;
    private bool _isWarningShown = false;
    private bool _hasTimedOut = false;

    public static NetworkHeartbeatManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        _lastHeartbeatReceived = Time.time;
        _heartbeatTimer = 0f;
        _hasTimedOut = false;
        _isWarningShown = false;

        if (connectionWarningUI != null)
        {
            connectionWarningUI.SetActive(false);
        }

        Debug.Log($"[NetworkHeartbeat] Spawned. IsServer: {IsServer}, IsClient: {IsClient}");
    }

    private void Update()
    {
        if (!IsSpawned) return;

        if (IsServer)
        {
            UpdateServerHeartbeat();
        }
        else
        {
            UpdateClientTimeout();
        }
    }

    /// <summary>
    /// Server: Send heartbeat to all clients periodically
    /// </summary>
    private void UpdateServerHeartbeat()
    {
        _heartbeatTimer += Time.deltaTime;

        if (_heartbeatTimer >= heartbeatInterval)
        {
            _heartbeatTimer = 0f;
            SendHeartbeatClientRpc();
        }
    }

    /// <summary>
    /// Client: Check if heartbeat has timed out
    /// </summary>
    private void UpdateClientTimeout()
    {
        if (_hasTimedOut) return;

        float timeSinceLastHeartbeat = Time.time - _lastHeartbeatReceived;

        // Show warning if connection is degrading
        if (timeSinceLastHeartbeat >= warningThreshold && !_isWarningShown)
        {
            _isWarningShown = true;
            ShowConnectionWarning(true);
            Debug.LogWarning($"[NetworkHeartbeat] Connection warning! {timeSinceLastHeartbeat:F1}s since last heartbeat");
        }

        // Timeout - return to menu
        if (timeSinceLastHeartbeat >= timeoutDuration)
        {
            _hasTimedOut = true;
            Debug.LogError($"[NetworkHeartbeat] Connection timeout! {timeoutDuration}s without heartbeat. Returning to menu...");
            HandleTimeout();
        }
    }

    [ClientRpc]
    private void SendHeartbeatClientRpc()
    {
        _lastHeartbeatReceived = Time.time;

        // Hide warning if connection restored
        if (_isWarningShown)
        {
            _isWarningShown = false;
            ShowConnectionWarning(false);
            Debug.Log("[NetworkHeartbeat] Connection restored!");
        }
    }

    private void ShowConnectionWarning(bool show)
    {
        if (connectionWarningUI != null)
        {
            connectionWarningUI.SetActive(show);
        }

        // You can also publish an event for other UI systems to handle
        // WorldRunner.Instance?.World?.Events?.Publish(new ConnectionWarningEvent { IsWarning = show });
    }

    private void HandleTimeout()
    {
        ShowConnectionWarning(false);

        // Shutdown network
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.Shutdown();
        }

        // Small delay to ensure cleanup
        StartCoroutine(ReturnToMenuDelayed());
    }

    private IEnumerator ReturnToMenuDelayed()
    {
        yield return new WaitForSeconds(0.5f);

        // Load menu scene
        SceneManager.LoadScene("Menu");
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();

        if (connectionWarningUI != null)
        {
            connectionWarningUI.SetActive(false);
        }
    }

    /// <summary>
    /// Call this to manually reset heartbeat timer (e.g., when receiving important RPCs)
    /// </summary>
    public void RefreshHeartbeat()
    {
        _lastHeartbeatReceived = Time.time;
    }
}
