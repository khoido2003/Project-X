using System.Collections;
using Mirror;
using UnityEngine;

public class GameNetworkManager : NetworkManager
{
    [Header("LAN discovery")]
    public string broadcastKey = "MYGAME_LAN";

    public GameObject gameSessionPrefab;

    public override void Awake()
    {
        base.Awake();
        if (gameSessionPrefab != null)
            NetworkClient.RegisterPrefab(gameSessionPrefab);
    }

    public override void OnServerAddPlayer(NetworkConnectionToClient conn)
    {
        base.OnServerAddPlayer(conn);
        Debug.Log($"Player {conn.connectionId} joined room (server)");
    }

    public override void OnClientConnect()
    {
        base.OnClientConnect();

        // When a client connects, show map selection first then character selection in UI
        if (UIManager.Instance != null)
        {
            UIManager.Instance.SetState(UIManagerState.MapSelection);
        }
    }

    /// <summary>
    /// Start host and call GameSession.StartHost on server object.
    /// </summary>
    public void StartHostRoom(string roomName)
    {
        Debug.Log("[GameNetworkManager] Starting host...");
        StartHost();
        // Start coroutine to wait for server to be ready then spawn session
        StartCoroutine(WaitAndSpawnSession(roomName));
    }

    private System.Collections.IEnumerator WaitAndSpawnSession(string roomName)
    {
        // Wait until NetworkServer is active (timeout optional)
        float timeout = 5f;
        float t = 0f;
        while (!NetworkServer.active && t < timeout)
        {
            t += Time.deltaTime;
            yield return null;
        }

        if (!NetworkServer.active)
        {
            Debug.LogError("[GameNetworkManager] Server not active after StartHost() (timeout)!");
            yield break;
        }

        if (gameSessionPrefab == null)
        {
            Debug.LogError("[GameNetworkManager] No GameSession prefab assigned!");
            yield break;
        }

        if (GameSession.Instance == null)
        {
            GameObject sessionObj = Instantiate(gameSessionPrefab);
            var session = sessionObj.GetComponent<GameSession>();
            session.roomName = roomName;
            NetworkServer.Spawn(sessionObj);
            session.StartHost(roomName);
            Debug.Log($"[GameNetworkManager] GameSession spawned: {roomName}");

            LANDiscovery.BroadcastRoom(
                roomName,
                NetworkServer.connections.Count, // current player count
                session.maxPlayers
            );
        }
        else
        {
            Debug.LogWarning("[GameNetworkManager] GameSession already exists!");
        }
    }

    /// <summary>
    /// Join a room by ip.
    /// </summary>
    public void JoinRoom(string ip)
    {
        networkAddress = ip;
        StartClient();
    }
}
