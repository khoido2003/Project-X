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
        if (LoadingSceneManager.Instance.SceneActive == SceneName.CharacterSelection)
        {
            int playersConnected = NetworkManager.Singleton.ConnectedClientsList.Count;

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
                return false;
            }
        }
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
