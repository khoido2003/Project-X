using Mirror;
using UnityEngine;

public class NetworkSessionController : MonoBehaviour
{
    public static NetworkSessionController Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }

        DontDestroyOnLoad(gameObject);
    }

    public void StartHost()
    {
        var nm = NetworkManager.singleton;
        if (!NetworkServer.active && !NetworkClient.active)
        {
            nm.StartHost();
        }
    }

    public void StopNetwork()
    {
        var nm = NetworkManager.singleton;
        if (NetworkServer.active)
        {
            nm.StopHost();
        }
        else if (NetworkClient.isConnected)
        {
            nm.StopClient();
        }
    }

    public void StartClient()
    {
        var nm = NetworkManager.singleton;
        if (nm == null)
        {
            return;
        }

        if (!NetworkClient.isConnected && !NetworkServer.active)
        {
            nm.StartClient();
            Debug.Log("Mirror client started.");
        }
    }

    public void StartServerMatch(string sceneName)
    {
        if (NetworkServer.active)
        {
            NetworkManager.singleton.ServerChangeScene(sceneName);
        }
    }
}
