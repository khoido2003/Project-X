using Unity.Netcode;
using UnityEngine;

public class ClientConnection : SingletonNetworkPersistent<ClientConnection>
{
    [SerializeField]
    private int m_maxConnections;

    [SerializeField]
    private CharacterDefinitionSO[] m_characterDatas;

    public bool IsExtraClient(ulong clientId)
    {
        return CanConnect(clientId);
    }

    public bool CanClientConnect(ulong clientId)
    {
        if (!IsServer)
        {
            return false;
        }

        bool canConnect = CanConnect(clientId);

        if (!canConnect)
        {
            RemoveClient(clientId);
        }

        return canConnect;
    }

    private bool CanConnect(ulong clientId)
    {
        // Spectators are always allowed to connect (they don't take player slots)
        if (SpectatorNetworkHandler.Instance != null && SpectatorNetworkHandler.Instance.IsSpectator(clientId))
        {
            Debug.Log($"[ClientConnection] Spectator {clientId} allowed to connect");
            return true;
        }
        
        // During character selection or loading transition, allow connection based on max players
        SceneName currentScene = LoadingSceneManager.Instance.SceneActive;
        if (currentScene == SceneName.CharacterSelection || currentScene == SceneName.Loading)
        {
            // Count only non-spectator players for max connection check
            int playersConnected = GetActivePlayerCount();

            if (playersConnected > m_maxConnections)
            {
                return false;
            }

            return true;
        }
        else
        {
            // Game is in progress (Map_1/2/3 etc)
            // For players who were already in the game, they have a character selected
            if (HasACharacterSelected(clientId))
            {
                return true;
            }
            
            // LATE-JOIN HANDLING:
            // If we reach here, this could be:
            // 1. A spectator whose RPC hasn't arrived yet (race condition)
            // 2. A late-joining player without a character (should be rejected)
            //
            // We temporarily allow connection here and let LoadingSceneManager
            // handle the proper verification with a delay. If they're not a spectator
            // after the delay, they'll be disconnected there.
            //
            // For now, we allow late-joiners through if we're in a gameplay scene
            // The SpectatorNetworkHandler will validate them properly.
            Debug.Log($"[ClientConnection] Client {clientId} connecting late to scene {currentScene} - allowing for spectator check");
            return true;
        }
    }
    
    /// <summary>
    /// Get count of connected players, excluding spectators.
    /// </summary>
    private int GetActivePlayerCount()
    {
        int count = 0;
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (SpectatorNetworkHandler.Instance == null || 
                !SpectatorNetworkHandler.Instance.IsSpectator(client.ClientId))
            {
                count++;
            }
        }
        return count;
    }

    private void RemoveClient(ulong clientId)
    {
        ClientRpcParams clientRpcParams = new()
        {
            Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { clientId } },
        };

        ShutdownClientRpc(clientRpcParams);
    }

    private bool HasACharacterSelected(ulong clientId)
    {
        foreach (var data in m_characterDatas)
        {
            if (data.clientId == clientId)
            {
                return true;
            }
        }

        return false;
    }

    [ClientRpc]
    private void ShutdownClientRpc(ClientRpcParams clientRpcParams = default)
    {
        Shutdown();
    }

    private void Shutdown()
    {
        NetworkManager.Singleton.Shutdown();
        LoadingSceneManager.Instance.LoadScene(SceneName.Menu, false);
    }
}
