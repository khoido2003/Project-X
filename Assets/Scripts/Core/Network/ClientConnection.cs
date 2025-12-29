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
            if (HasACharacterSelected(clientId))
            {
                return true;
            }
            else
            {
                Debug.LogWarning($"[ClientConnection] Client {clientId} rejected - no character selected. Scene: {currentScene}");
                return false;
            }
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
