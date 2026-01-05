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

    [Header("Spectator Mode")]
    [SerializeField]
    private Button m_watchMatchBtn;

    [SerializeField]
    private GameObject m_spectatorPanel;

    [SerializeField]
    private TMP_InputField m_spectatorIpInputField;

    [SerializeField]
    private Button m_spectatorConnectBtn;

    [SerializeField]
    private Button m_spectatorCancelBtn;

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

    [Header("Error Notification")]
    [SerializeField]
    [Tooltip("Panel to show error messages (host failure, etc.)")]
    private GameObject m_errorPanel;

    [SerializeField]
    private TextMeshProUGUI m_errorText;

    [SerializeField]
    private Button m_errorOkBtn;

    [Header("Reconnect Panel")]
    [SerializeField]
    [Tooltip("Panel shown after disconnect with reconnect option")]
    private GameObject m_reconnectPanel;

    [SerializeField]
    private TextMeshProUGUI m_reconnectText;

    [SerializeField]
    private Button m_reconnectBtn;

    [SerializeField]
    private Button m_reconnectCancelBtn;

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

        // Setup spectator button
        if (m_watchMatchBtn != null)
        {
            m_watchMatchBtn.onClick.AddListener(OnClickWatchMatch);
        }

        // Setup spectator panel buttons
        if (m_spectatorConnectBtn != null)
        {
            m_spectatorConnectBtn.onClick.AddListener(OnClickSpectatorConnect);
        }

        if (m_spectatorCancelBtn != null)
        {
            m_spectatorCancelBtn.onClick.AddListener(OnClickSpectatorCancel);
        }

        // Setup join panel buttons if assigned
        if (m_connectBtn != null)
        {
            m_connectBtn.onClick.AddListener(OnClickConnect);
        }

        if (m_cancelJoinBtn != null)
        {
            m_cancelJoinBtn.onClick.AddListener(OnClickCancelJoin);
        }

        // Setup error panel button
        if (m_errorOkBtn != null)
        {
            m_errorOkBtn.onClick.AddListener(OnClickErrorOk);
        }

        // Setup reconnect panel buttons
        if (m_reconnectBtn != null)
        {
            m_reconnectBtn.onClick.AddListener(OnClickReconnect);
        }

        if (m_reconnectCancelBtn != null)
        {
            m_reconnectCancelBtn.onClick.AddListener(OnClickReconnectCancel);
        }

        // Hide panels initially
        if (m_joinPanel != null)
        {
            m_joinPanel.SetActive(false);
        }

        if (m_spectatorPanel != null)
        {
            m_spectatorPanel.SetActive(false);
        }

        if (m_errorPanel != null)
        {
            m_errorPanel.SetActive(false);
        }

        if (m_reconnectPanel != null)
        {
            m_reconnectPanel.SetActive(false);
        }

        m_hostBtn.gameObject.SetActive(false);
        m_joinBtn.gameObject.SetActive(false);
        m_quickGameBtn.gameObject.SetActive(false);
        m_reconnectBtn.gameObject.SetActive(false);

        if (m_watchMatchBtn != null)
            m_watchMatchBtn.gameObject.SetActive(false);

        yield return new WaitUntil(() => NetworkManager.Singleton.SceneManager != null);
        LoadingSceneManager.Instance.Init();

        // Check for reconnection opportunity after returning from a disconnect
        if (ConnectionSettings.IsReconnectionAttempt)
        {
            ShowReconnectOption();
        }
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

        // Try to start host and check for failure
        bool success = NetworkManager.Singleton.StartHost();

        if (!success)
        {
            Debug.LogError($"[MenuManager] Failed to start host on port {m_port}. Port may already be in use.");
            ShowHostErrorNotification(
                $"Failed to host on port {m_port}.\n\nAnother game may already be running on this network, or the port is in use by another application."
            );
            return;
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

        LoadingSceneManager.Instance.LoadScene(nextScene);
    }

    #region Error and Reconnect UI

    private void ShowHostErrorNotification(string message)
    {
        if (m_errorPanel != null)
        {
            m_errorPanel.SetActive(true);
            if (m_errorText != null)
                m_errorText.text = message;
            HideMainButtons();
        }
        else
        {
            // Fallback: just log to console if no UI panel
            Debug.LogError($"[MenuManager] HOST FAILED: {message}");
            // Still allow user to try again by keeping buttons visible
        }
    }

    /// <summary>
    /// Shows notification when connection to host fails (no match found).
    /// </summary>
    private void ShowConnectionFailedNotification(string ipAddress, bool isSpectator)
    {
        string mode = isSpectator ? "watch" : "join";
        string message =
            $"Could not connect to {ipAddress}:{m_port}.\n\nNo match is currently running at this address, or the host may have closed the game.";

        if (m_errorPanel != null)
        {
            m_errorPanel.SetActive(true);
            if (m_errorText != null)
                m_errorText.text = message;
            HideMainButtons();
        }
        else
        {
            Debug.LogWarning($"[MenuManager] CONNECTION FAILED: {message}");
            ShowMainButtons();
        }
    }

    private void OnClickErrorOk()
    {
        PlayButtonClickSound();

        if (m_errorPanel != null)
        {
            m_errorPanel.SetActive(false);
        }

        ShowMainButtons();
    }

    private void ShowReconnectOption()
    {
        if (m_reconnectPanel != null)
        {
            string message;
            if (ConnectionSettings.IsSpectator)
            {
                // Spectator reconnection is supported
                message =
                    $"You were disconnected from {ConnectionSettings.TargetIP}.\n\nWould you like to reconnect as spectator?";
            }
            else
            {
                // Player reconnection is NOT supported - inform user
                message =
                    $"You were disconnected from {ConnectionSettings.TargetIP}.\n\nNote: Player reconnection is not supported. You can only rejoin as a spectator.";
            }

            if (m_reconnectText != null)
            {
                m_reconnectText.text = message;
            }
            m_reconnectPanel.SetActive(true);
            // Skip the press any key phase
            m_pressAnyKeyActive = false;
            if (m_pressAnyKeyText != null)
                m_pressAnyKeyText.gameObject.SetActive(false);
            // Show main buttons behind the reconnect panel so user can dismiss and still navigate
            ShowMainButtons();
        }
        else
        {
            Debug.Log(
                $"[MenuManager] Reconnection opportunity: {ConnectionSettings.TargetIP} (Spectator: {ConnectionSettings.IsSpectator})"
            );
            // No UI panel, just reset and continue normally
            ConnectionSettings.Reset();
        }
    }

    private void OnClickReconnect()
    {
        PlayButtonClickSound();

        if (m_reconnectPanel != null)
        {
            m_reconnectPanel.SetActive(false);
        }

        // Only allow spectator reconnection - player reconnection is not supported
        if (ConnectionSettings.IsSpectator)
        {
            StartCoroutine(JoinAsSpectator(ConnectionSettings.TargetIP));
        }
        else
        {
            // Player reconnection is not supported - show error
            string message =
                "Player reconnection is not supported.\n\nYour character slot was lost when you disconnected. You can rejoin as a spectator to watch the match.";

            if (m_errorPanel != null)
            {
                m_errorPanel.SetActive(true);
                if (m_errorText != null)
                    m_errorText.text = message;
                HideMainButtons();
            }
            else
            {
                Debug.LogWarning($"[MenuManager] {message}");
                ShowMainButtons();
            }

            // Clear connection settings since we can't reconnect as player
            ConnectionSettings.Reset();
            return;
        }

        // Clear reconnection flag (will be set again if disconnect happens)
        ConnectionSettings.IsReconnectionAttempt = false;
    }

    private void OnClickReconnectCancel()
    {
        PlayButtonClickSound();

        if (m_reconnectPanel != null)
        {
            m_reconnectPanel.SetActive(false);
        }

        // Clear all connection settings
        ConnectionSettings.Reset();

        ShowMainButtons();
    }

    #endregion

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
            m_reconnectBtn.gameObject.SetActive(false);

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
        m_reconnectBtn.gameObject.SetActive(true);
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
        m_reconnectBtn.gameObject.SetActive(true);
        if (m_watchMatchBtn != null)
            m_watchMatchBtn.gameObject.SetActive(true);
    }

    #region Spectator Mode

    /// <summary>
    /// Called when Watch Match button is clicked.
    /// Shows the spectator IP input panel.
    /// </summary>
    public void OnClickWatchMatch()
    {
        PlayButtonClickSound();

        if (m_spectatorPanel != null && m_spectatorIpInputField != null)
        {
            m_spectatorPanel.SetActive(true);
            HideMainButtons();

            // Set default IP if empty
            if (string.IsNullOrEmpty(m_spectatorIpInputField.text))
            {
                m_spectatorIpInputField.text = "127.0.0.1";
            }
        }
        else
        {
            // Fallback: direct connect with default IP
            StartCoroutine(JoinAsSpectator("127.0.0.1"));
        }
    }

    private void OnClickSpectatorConnect()
    {
        PlayButtonClickSound();

        string ipAddress = m_spectatorIpInputField.text.Trim();
        if (string.IsNullOrEmpty(ipAddress))
        {
            Debug.LogWarning("[MenuManager] Spectator IP address is empty!");
            return;
        }

        if (m_spectatorPanel != null)
        {
            m_spectatorPanel.SetActive(false);
        }

        StartCoroutine(JoinAsSpectator(ipAddress));
    }

    private void OnClickSpectatorCancel()
    {
        PlayButtonClickSound();

        if (m_spectatorPanel != null)
        {
            m_spectatorPanel.SetActive(false);
        }

        ShowMainButtons();
    }

    /// <summary>
    /// Join as spectator - sets flag and connects directly to game scene.
    /// </summary>
    private IEnumerator JoinAsSpectator(string ipAddress)
    {
        LoadingFadeEffect.Instance.FadeAll();
        yield return new WaitUntil(() => LoadingFadeEffect.s_canLoad);

        // Set spectator flag BEFORE connecting
        ConnectionSettings.IsSpectator = true;
        ConnectionSettings.TargetIP = ipAddress;

        // Configure transport
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        if (transport != null)
        {
            transport.SetConnectionData(ipAddress, m_port);
            Debug.Log($"[MenuManager] Spectator connecting to {ipAddress}:{m_port}");
        }

        // Subscribe to connection events
        NetworkManager.Singleton.OnClientDisconnectCallback += OnSpectatorDisconnected;
        NetworkManager.Singleton.OnClientConnectedCallback += OnSpectatorConnected;

        NetworkManager.Singleton.StartClient();

        // Wait for connection
        yield return new WaitForSeconds(5f);

        if (!NetworkManager.Singleton.IsConnectedClient)
        {
            Debug.LogWarning($"[MenuManager] Spectator failed to connect to {ipAddress}:{m_port}");
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnSpectatorDisconnected;
            NetworkManager.Singleton.OnClientConnectedCallback -= OnSpectatorConnected;
            NetworkManager.Singleton.Shutdown();
            ConnectionSettings.Reset();
            LoadingFadeEffect.Instance.FadeOut();

            // Show error notification
            ShowConnectionFailedNotification(ipAddress, true);
        }
    }

    private void OnSpectatorConnected(ulong clientId)
    {
        Debug.Log("[MenuManager] Spectator connected successfully!");
        NetworkManager.Singleton.OnClientDisconnectCallback -= OnSpectatorDisconnected;
        NetworkManager.Singleton.OnClientConnectedCallback -= OnSpectatorConnected;
        // Note: Scene will be loaded by the host's scene manager
    }

    private void OnSpectatorDisconnected(ulong clientId)
    {
        if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsServer)
        {
            Debug.LogWarning("[MenuManager] Spectator disconnected from host.");
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnSpectatorDisconnected;
            NetworkManager.Singleton.OnClientConnectedCallback -= OnSpectatorConnected;
            ConnectionSettings.Reset();

            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.Shutdown();
            }

            LoadingFadeEffect.Instance.FadeOut();
            LoadingSceneManager.Instance.LoadScene(SceneName.Menu, false);
        }
    }

    private void HideMainButtons()
    {
        m_hostBtn.gameObject.SetActive(false);
        m_joinBtn.gameObject.SetActive(false);
        m_reconnectBtn.gameObject.SetActive(false);
        m_quickGameBtn.gameObject.SetActive(false);

        if (m_watchMatchBtn != null)
            m_watchMatchBtn.gameObject.SetActive(false);
    }

    private void ShowMainButtons()
    {
        m_hostBtn.gameObject.SetActive(true);
        m_joinBtn.gameObject.SetActive(true);
        m_reconnectBtn.gameObject.SetActive(true);
        m_quickGameBtn.gameObject.SetActive(true);

        if (m_watchMatchBtn != null)
            m_watchMatchBtn.gameObject.SetActive(true);
    }

    #endregion

    private IEnumerator Join(string ipAddress)
    {
        LoadingFadeEffect.Instance.FadeAll();

        yield return new WaitUntil(() => LoadingFadeEffect.s_canLoad);

        // Store target IP for potential reconnection
        ConnectionSettings.TargetIP = ipAddress;

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
            ConnectionSettings.Reset();
            LoadingFadeEffect.Instance.FadeOut();

            // Show error notification
            ShowConnectionFailedNotification(ipAddress, false);
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
