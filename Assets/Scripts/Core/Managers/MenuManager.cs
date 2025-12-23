using System.Collections;
using System.Net;
using System.Net.Sockets;
using System.Text;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.UI;

public class MenuManager : MonoBehaviour
{
    [SerializeField]
    private CharacterDefinitionSO[] m_characterDatas;

    [SerializeField]
    private AudioClip m_confirmClip;

    private bool m_pressAnyKeyActive = true;

    [SerializeField]
    private SceneName nextScene = SceneName.CharacterSelection;

    [SerializeField]
    private TextMeshProUGUI m_pressAnyKeyText;

    [SerializeField]
    private Button m_hostBtn;

    [SerializeField]
    private Button m_joinBtn;

    [SerializeField]
    private Button m_quickGameBtn;

    [SerializeField]
    private UISoundConfig uiSoundConfig;

    [Header("Join Panel")]
    [SerializeField]
    private GameObject m_joinPanel;

    [SerializeField]
    private TMP_InputField m_ipInputField;

    [SerializeField]
    private Button m_connectBtn;

    [SerializeField]
    private Button m_cancelJoinBtn;

    [SerializeField]
    private ushort m_port = 7777;

    [Header("Host IP Display")]
    [SerializeField]
    [Tooltip("Optional: TextMeshPro to display host IP addresses for sharing with clients")]
    private TextMeshProUGUI m_hostIpDisplay;

    private void Awake() { }

    private IEnumerator Start()
    {
        ClearAllCharactersData();

        m_hostBtn.onClick.AddListener(() =>
        {
            OnClickHost();
        });

        m_joinBtn.onClick.AddListener(() =>
        {
            OnClickJoin();
        });

        m_quickGameBtn.onClick.AddListener(() =>
        {
            OnClickQuit();
        });

        // Setup join panel buttons if assigned
        if (m_connectBtn != null)
        {
            m_connectBtn.onClick.AddListener(OnClickConnect);
        }

        if (m_cancelJoinBtn != null)
        {
            m_cancelJoinBtn.onClick.AddListener(OnClickCancelJoin);
        }

        // Hide join panel initially
        if (m_joinPanel != null)
        {
            m_joinPanel.SetActive(false);
        }

        m_hostBtn.gameObject.SetActive(false);
        m_joinBtn.gameObject.SetActive(false);
        m_quickGameBtn.gameObject.SetActive(false);

        yield return new WaitUntil(() => NetworkManager.Singleton.SceneManager != null);
        LoadingSceneManager.Instance.Init();
    }

    private void Update()
    {
        if (m_pressAnyKeyActive)
        {
            if (Input.anyKey)
            {
                TriggerMainMenuTransition();
                m_pressAnyKeyActive = false;
            }
        }
    }

    public void OnClickHost()
    {
        PlayButtonClickSound();

        // Configure transport to listen on all network interfaces
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        if (transport != null)
        {
            // Listen on all interfaces (0.0.0.0) so clients can connect via any network adapter
            transport.SetConnectionData("0.0.0.0", m_port, "0.0.0.0");
        }

        // Get and display local IP addresses
        string ipInfo = GetLocalIPAddresses();
        Debug.Log($"[MenuManager] Host starting on port {m_port}. Share one of these IPs with clients:\n{ipInfo}");

        // Display IPs on screen if UI element exists
        if (m_hostIpDisplay != null)
        {
            m_hostIpDisplay.text = $"Your IP (share with clients):\n{ipInfo}";
            m_hostIpDisplay.gameObject.SetActive(true);
        }

        NetworkManager.Singleton.StartHost();
        LoadingSceneManager.Instance.LoadScene(nextScene);
    }

    public void OnClickJoin()
    {
        PlayButtonClickSound();

        // If join panel exists, show it for IP input
        if (m_joinPanel != null && m_ipInputField != null)
        {
            m_joinPanel.SetActive(true);
            m_hostBtn.gameObject.SetActive(false);
            m_joinBtn.gameObject.SetActive(false);
            m_quickGameBtn.gameObject.SetActive(false);

            // Set default IP if empty
            if (string.IsNullOrEmpty(m_ipInputField.text))
            {
                m_ipInputField.text = "127.0.0.1";
            }
        }
        else
        {
            // Fallback: direct connect with default IP (for backwards compatibility)
            StartCoroutine(Join("127.0.0.1"));
        }
    }

    private void OnClickConnect()
    {
        PlayButtonClickSound();

        string ipAddress = m_ipInputField.text.Trim();
        if (string.IsNullOrEmpty(ipAddress))
        {
            Debug.LogWarning("[MenuManager] IP address is empty!");
            return;
        }

        if (m_joinPanel != null)
        {
            m_joinPanel.SetActive(false);
        }

        StartCoroutine(Join(ipAddress));
    }

    private void OnClickCancelJoin()
    {
        PlayButtonClickSound();

        if (m_joinPanel != null)
        {
            m_joinPanel.SetActive(false);
        }

        // Show main menu buttons again
        m_hostBtn.gameObject.SetActive(true);
        m_joinBtn.gameObject.SetActive(true);
        m_quickGameBtn.gameObject.SetActive(true);
    }

    public void OnClickQuit()
    {
        PlayButtonClickSound();
        Application.Quit();
    }

    private void PlayButtonClickSound()
    {
        if (uiSoundConfig != null && uiSoundConfig.buttonClick != null && AudioService.Instance != null)
        {
            AudioHelper.PlaySound(uiSoundConfig.buttonClick, AudioCategory.UI, uiSoundConfig.uiSoundVolume);
        }
        else if (m_confirmClip != null && AudioService.Instance != null)
        {
            // Fallback to legacy clip
            AudioHelper.PlaySound(m_confirmClip, AudioCategory.UI);
        }
    }

    private void ClearAllCharactersData()
    {
        foreach (CharacterDefinitionSO data in m_characterDatas)
        {
            data.EmptyData();
        }
    }

    private void TriggerMainMenuTransition()
    {
        m_pressAnyKeyText.gameObject.SetActive(false);

        m_hostBtn.gameObject.SetActive(true);
        m_joinBtn.gameObject.SetActive(true);
        m_quickGameBtn.gameObject.SetActive(true);
    }

    private IEnumerator Join(string ipAddress)
    {
        LoadingFadeEffect.Instance.FadeAll();

        yield return new WaitUntil(() => LoadingFadeEffect.s_canLoad);

        // Configure transport with the target host IP address
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        if (transport != null)
        {
            transport.SetConnectionData(ipAddress, m_port);
            Debug.Log($"[MenuManager] Client connecting to {ipAddress}:{m_port}");
        }

        // Subscribe to connection/disconnection events
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;

        NetworkManager.Singleton.StartClient();

        // Wait a bit to see if connection succeeds
        yield return new WaitForSeconds(5f);

        // If still not connected, show error and return to menu
        if (!NetworkManager.Singleton.IsConnectedClient)
        {
            Debug.LogWarning($"[MenuManager] Failed to connect to host at {ipAddress}:{m_port}. Returning to menu.");
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.Shutdown();
            LoadingFadeEffect.Instance.FadeOut();

            // Show main menu buttons again
            m_hostBtn.gameObject.SetActive(true);
            m_joinBtn.gameObject.SetActive(true);
            m_quickGameBtn.gameObject.SetActive(true);
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        // Connection successful, unsubscribe from events
        NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
    }

    private void OnClientDisconnected(ulong clientId)
    {
        // Only handle if we're the disconnected client (not server disconnecting us)
        if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsServer)
        {
            Debug.LogWarning("Disconnected from host. Returning to menu.");
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;

            // Shutdown and return to menu
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.Shutdown();
            }

            LoadingFadeEffect.Instance.FadeOut();
            LoadingSceneManager.Instance.LoadScene(SceneName.Menu, false);
        }
    }

    /// <summary>
    /// Gets all local IPv4 addresses from network adapters.
    /// Useful for finding Radmin VPN IP (usually starts with 26.x.x.x).
    /// </summary>
    private string GetLocalIPAddresses()
    {
        StringBuilder sb = new StringBuilder();

        try
        {
            // Get host name
            string hostName = Dns.GetHostName();
            IPHostEntry hostEntry = Dns.GetHostEntry(hostName);

            foreach (IPAddress ip in hostEntry.AddressList)
            {
                // Only include IPv4 addresses
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    string ipStr = ip.ToString();

                    // Skip loopback
                    if (ipStr.StartsWith("127."))
                        continue;

                    // Add label hint for common VPN ranges
                    string label = "";
                    if (ipStr.StartsWith("26."))
                        label = " (Radmin VPN)";
                    else if (ipStr.StartsWith("10.") || ipStr.StartsWith("172.") || ipStr.StartsWith("192.168."))
                        label = " (LAN)";

                    sb.AppendLine($"• {ipStr}{label}");
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[MenuManager] Failed to get local IPs: {e.Message}");
            sb.AppendLine("Unable to detect IP");
        }

        if (sb.Length == 0)
        {
            sb.AppendLine("No network adapters found");
        }

        return sb.ToString().TrimEnd();
    }
}
