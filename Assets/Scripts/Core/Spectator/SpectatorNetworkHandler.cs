using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Singleton NetworkBehaviour that tracks spectator clients.
/// This must be attached to a PERSISTENT GameObject (like NetworkManager).
/// 
/// The flow is:
/// 1. Spectator sets ConnectionSettings.IsSpectator = true in Menu
/// 2. Spectator connects to host
/// 3. On connection, client sends RegisterAsSpectatorServerRpc IMMEDIATELY
/// 4. Server adds client to spectator list BEFORE any scene loads
/// 5. LoadingSceneManager checks IsSpectator() to skip player spawning
/// </summary>
public class SpectatorNetworkHandler : NetworkBehaviour
{
    public static SpectatorNetworkHandler Instance { get; private set; }

    // Server-side: Track which client IDs are spectators
    private HashSet<ulong> _spectatorClientIds = new();

    // Client-side: Has this client registered as spectator yet?
    private bool _hasRegistered = false;

    private void Awake()
    {
        // Ensure singleton persists across scenes
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        
        // Make persistent if not already
        if (transform.parent == null)
        {
            DontDestroyOnLoad(gameObject);
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // Client: If we're a spectator, notify the server IMMEDIATELY
        // This must happen as soon as we connect, before any scene transitions
        if (IsClient && !IsServer && ConnectionSettings.IsSpectator && !_hasRegistered)
        {
            _hasRegistered = true;
            RegisterAsSpectatorServerRpc();
            Debug.Log("[SpectatorNetworkHandler] Client registering as spectator with server");
        }

        // Server: Subscribe to client disconnect to cleanup
        if (IsServer && NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        }
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();

        if (IsServer && NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
        
        // Reset registration flag in case we reconnect later
        _hasRegistered = false;
    }
    
    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void OnClientDisconnected(ulong clientId)
    {
        _spectatorClientIds.Remove(clientId);
        Debug.Log($"[SpectatorNetworkHandler] Removed spectator status for disconnected client {clientId}");
    }

    /// <summary>
    /// Called by spectator clients to register themselves with the server.
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    private void RegisterAsSpectatorServerRpc(ServerRpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;
        _spectatorClientIds.Add(clientId);
        Debug.Log($"[SpectatorNetworkHandler] Client {clientId} registered as spectator. Total spectators: {_spectatorClientIds.Count}");
    }

    /// <summary>
    /// Check if a client is a spectator.
    /// Call this on the server to determine if player should be spawned.
    /// </summary>
    public bool IsSpectator(ulong clientId)
    {
        return _spectatorClientIds.Contains(clientId);
    }

    /// <summary>
    /// Get the number of spectators currently connected.
    /// </summary>
    public int SpectatorCount => _spectatorClientIds.Count;

    /// <summary>
    /// Get all spectator client IDs (for UI display, etc.)
    /// </summary>
    public IEnumerable<ulong> GetSpectatorClientIds()
    {
        return _spectatorClientIds;
    }
    
    /// <summary>
    /// Clear all spectator data. Call when returning to menu.
    /// </summary>
    public void ClearAll()
    {
        _spectatorClientIds.Clear();
        _hasRegistered = false;
    }
}
