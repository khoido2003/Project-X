using System;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public enum ConnectionState : byte
{
    connected,
    disconnected,
    ready,
}

[Serializable]
public struct PlayerConnectionState
{
    public ulong clientId;
    public string playerName;
    public ConnectionState playerState;
    public PlayerCharSelection playerObject;
}

[Serializable]
public struct CharacterContainer
{
    public Image imageContainer;
    public TextMeshProUGUI nameContainer;
    public GameObject border;
    public GameObject borderClient;
    public GameObject borderReady;
    public Image playerIcon;
    public GameObject waitingText;

    public GameObject backgroundWeapon;
    public Image backgroundWeaponImage;
    public GameObject backgroundWeaponReady;
    public Image backgroundWeaponReadyImage;
    public GameObject backgroundClientWeaponReady;
    public Image backgroundClientWeaponReadyImage;
}

public class CharacterSelectionManager : SingletonNetwork<CharacterSelectionManager>
{
    public CharacterDefinitionSO[] characterData;

    [SerializeField]
    private CharacterContainer[] m_charactersContainer;

    [SerializeField]
    private Button m_readyBtn;

    [SerializeField]
    private Button m_cancelBtn;
    
    [SerializeField]
    private Button m_exitRoomBtn;

    [SerializeField]
    private float m_timeToStartGame = 5;

    [SerializeField]
    private SceneName m_nextScene;

    [SerializeField]
    private Color m_clientColor;

    [SerializeField]
    private Color m_playerColor;

    [SerializeField]
    private PlayerConnectionState[] m_playerStates;

    [SerializeField]
    private GameObject m_playerPrefab;

    [SerializeField]
    private GameObject m_countdownContainer;

    [SerializeField]
    private TextMeshProUGUI m_countdownText;

    [Header("Audio")]
    [SerializeField]
    private UISoundConfig uiSoundConfig;

    [SerializeField]
    private VoiceoverConfig voiceoverConfig;

    [SerializeField]
    private AudioClip m_confirmClip;

    [SerializeField]
    private AudioClip m_cancelClip;

    private bool m_isTimerOn;
    private float m_timer;
    private int _lastCountdownPlayed = -1; // Track last countdown number played

    private readonly Color k_selectedColor = new Color32(74, 74, 74, 255);
    
    // Flag to prevent multiple shutdown calls
    private bool _isShuttingDown = false;

    private void Start()
    {
        m_countdownContainer.gameObject.SetActive(false);
        m_timer = m_timeToStartGame;
        RemoveSelectedStates();
        
        // Setup exit room button
        if (m_exitRoomBtn != null)
        {
            m_exitRoomBtn.onClick.AddListener(OnExitRoomClicked);
        }
    }

    private void Update()
    {
        if (!IsServer)
        {
            return;
        }
        
        // Don't process updates if we're shutting down
        if (_isShuttingDown || !IsSpawned)
        {
            return;
        }

        if (!m_isTimerOn)
        {
            return;
        }

        m_timer -= Time.deltaTime;

        if (m_timer < 0f)
        {
            m_timer = 0f;
        }

        int currentCountdown = Mathf.FloorToInt(m_timer);

        // Sync countdown to all clients
        UpdateCountdownClientRpc(currentCountdown, m_timer);

        // Play countdown voiceover
        if (currentCountdown >= 1 && currentCountdown <= 3 && currentCountdown != _lastCountdownPlayed)
        {
            _lastCountdownPlayed = currentCountdown;
            PlayCountdownVoiceoverClientRpc(currentCountdown);
        }

        // Play "Ready" at 1 second, "Game Start" at 0
        if (m_timer <= 1f && m_timer > 0f && _lastCountdownPlayed != 0)
        {
            if (voiceoverConfig != null && voiceoverConfig.ready != null && AudioService.Instance != null)
            {
                PlayReadyVoiceoverClientRpc();
                _lastCountdownPlayed = 0;
            }
        }

        if (m_timer <= 0f)
        {
            m_isTimerOn = false;
            _lastCountdownPlayed = -1;
            if (voiceoverConfig != null && voiceoverConfig.gameStart != null && AudioService.Instance != null)
            {
                PlayGameStartVoiceoverClientRpc();
            }
            StartGame();
        }
    }

    private void OnDisable()
    {
        if (NetworkManager.Singleton == null)
        {
            return;
        }

        if (NetworkManager.Singleton.ShutdownInProgress)
        {
            return;
        }

        // RemoveSelectedStates();

        if (IsServer)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback -= PlayerDisconnects;
        }
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback += PlayerDisconnects;
        }
    }

    //////////////////////////////////////////////////////////////

    #region Callbacks

    public void PlayerDisconnects(ulong clientId)
    {
        // Ignore if already shutting down
        if (_isShuttingDown)
        {
            return;
        }
        
        if (!ClientConnection.Instance.IsExtraClient(clientId))
        {
            return;
        }

        if (clientId == 0)
        {
            // Host disconnected - this is handled by OnExitRoomClicked, skip here to avoid double shutdown
            NetworkManager.Singleton.OnClientDisconnectCallback -= PlayerDisconnects;
            return;
        }

        PlayerNotReady(clientId, isDisconnected: true);
        m_playerStates[GetPlayerId(clientId)].playerObject.Despawn();
    }

    #endregion

    /////////////////////////////////////////////////////////////

    #region Start Game

    private void StartGame()
    {
        if (MapSelectionManager.Instance != null)
        {
            m_nextScene = MapSelectionManager.Instance.SelectedMapScene;
            Debug.Log($"Loading selected map: {m_nextScene}");
        }
        else
        {
            Debug.LogWarning("MapSelectionManager not found! Loading  default  scene to map_1");
        }

        StartGameClientRpc();
        LoadingSceneManager.Instance.LoadScene(m_nextScene);
    }

    private void StartGameTimer()
    {
        foreach (PlayerConnectionState state in m_playerStates)
        {
            if (state.playerState == ConnectionState.connected)
            {
                return;
            }
        }

        m_timer = m_timeToStartGame;
        m_isTimerOn = true;
        _lastCountdownPlayed = -1;

        // Notify all clients that countdown started
        StartCountdownClientRpc();
    }

    [ClientRpc]
    private void StartCountdownClientRpc()
    {
        if (m_countdownContainer != null)
        {
            m_countdownContainer.gameObject.SetActive(true);
        }

        if (m_countdownText != null)
        {
            m_countdownText.text = Mathf.FloorToInt(m_timeToStartGame).ToString();
        }
    }

    #endregion

    /////////////////////////////////////////////////////////////////

    #region Set States

    private void RemoveSelectedStates()
    {
        for (int i = 0; i < characterData.Length; i++)
        {
            characterData[i].isSelected = false;
        }
    }

    private void RemoveReadyStates(ulong clientId, bool disconnected)
    {
        for (int i = 0; i < m_playerStates.Length; i++)
        {
            if (m_playerStates[i].playerState == ConnectionState.ready && m_playerStates[i].clientId == clientId)
            {
                if (disconnected)
                {
                    m_playerStates[i].playerState = ConnectionState.disconnected;

                    UpdatePlayerStateClientRpc(clientId, i, ConnectionState.disconnected);
                }
                else
                {
                    m_playerStates[i].playerState = ConnectionState.connected;

                    UpdatePlayerStateClientRpc(clientId, i, ConnectionState.connected);
                }
            }
        }
    }

    public void PlayerNotReady(ulong clientId, int characterSelected = 0, bool isDisconnected = false)
    {
        int playerId = GetPlayerId(clientId);

        m_isTimerOn = false;
        m_timer = m_timeToStartGame;

        RemoveReadyStates(clientId, isDisconnected);

        if (isDisconnected)
        {
            PlayerDisconnectClientRpc(playerId);
        }
        else
        {
            PlayerNotReadyClientRpc(clientId, playerId, characterSelected);
        }
    }

    public void PlayerReady(ulong clientId, int playerId, int characterSelected)
    {
        if (!characterData[characterSelected].isSelected)
        {
            // Set on server immediately (don't rely on ClientRpc for server-side data)
            // This prevents race conditions with network latency (e.g., Radmin VPN)
            characterData[characterSelected].isSelected = true;
            characterData[characterSelected].clientId = clientId;
            characterData[characterSelected].playerId = playerId;
            
            PlayerReadyClientRpc(clientId, playerId, characterSelected);

            StartGameTimer();
        }
    }

    #endregion

    //////////////////////////////////////////////////////////////////

    #region Get States

    public int GetPlayerId(ulong clientId)
    {
        for (int i = 0; i < m_playerStates.Length; i++)
        {
            if (m_playerStates[i].clientId == clientId)
            {
                return i;
            }
        }
        Debug.LogError("This should never happen");

        return -1;
    }

    public ConnectionState GetConnectionState(int playerId)
    {
        if (playerId != -1)
        {
            return m_playerStates[playerId].playerState;
        }
        return ConnectionState.disconnected;
    }

    public bool IsSelectedByPlayer(int playerId, int characterSelected)
    {
        return characterData[characterSelected].playerId == playerId;
    }

    public bool IsReady(int playerId)
    {
        return characterData[playerId].isSelected;
    }

    /// <summary>
    /// Check if a specific client has a selected character (for late-join validation).
    /// </summary>
    public bool HasCharacterForClient(ulong clientId)
    {
        foreach (var data in characterData)
        {
            if (data.isSelected && data.clientId == clientId)
            {
                return true;
            }
        }
        return false;
    }

    #endregion

    ///////////////////////////////////////////////////////////////

    #region Setup UI


    public void ServerSceneInit(ulong clientId)
    {
        GameObject go = NetworkObjectSpawner.SpawnNewNetworkObjectChangeOwnershipToClient(
            m_playerPrefab,
            transform.position,
            clientId,
            true
        );

        for (int i = 0; i < m_playerStates.Length; i++)
        {
            if (m_playerStates[i].playerState == ConnectionState.disconnected)
            {
                m_playerStates[i].playerState = ConnectionState.connected;
                m_playerStates[i].playerObject = go.GetComponent<PlayerCharSelection>();
                m_playerStates[i].playerName = go.name;
                m_playerStates[i].clientId = clientId;

                break;
            }
        }

        for (int i = 0; i < m_playerStates.Length; i++)
        {
            if (m_playerStates[i].playerObject != null)
            {
                PlayerConnectsClientRpc(
                    m_playerStates[i].clientId,
                    i,
                    m_playerStates[i].playerState,
                    m_playerStates[i].playerObject.GetComponent<NetworkObject>()
                );
            }
        }
    }

    public void SetPlayerReadyUIButtons(bool isReady, int characterSelected)
    {
        if (isReady && !characterData[characterSelected].isSelected)
        {
            m_readyBtn.gameObject.SetActive(false);
            m_cancelBtn.gameObject.SetActive(true);
        }
        else if (!isReady && characterData[characterSelected].isSelected)
        {
            m_readyBtn.gameObject.SetActive(true);
            m_cancelBtn.gameObject.SetActive(false);
        }
    }

    public void SetPlayableChar(int playerId, int characterSelected, bool isClientOwner)
    {
        SetCharacterUI(playerId, characterSelected);

        m_charactersContainer[playerId].playerIcon.gameObject.SetActive(true);

        if (isClientOwner)
        {
            m_charactersContainer[playerId].borderClient.SetActive(true);
            m_charactersContainer[playerId].border.SetActive(false);
            m_charactersContainer[playerId].borderReady.SetActive(false);
            m_charactersContainer[playerId].playerIcon.color = m_clientColor;
        }
        else
        {
            m_charactersContainer[playerId].border.SetActive(true);
            m_charactersContainer[playerId].borderReady.SetActive(false);
            m_charactersContainer[playerId].borderClient.SetActive(false);
            m_charactersContainer[playerId].playerIcon.color = m_playerColor;
        }

        m_charactersContainer[playerId].backgroundWeapon.SetActive(true);
        m_charactersContainer[playerId].waitingText.SetActive(false);
    }

    private void SetNonPlayableChar(int playerId)
    {
        m_charactersContainer[playerId].imageContainer.sprite = null;
        m_charactersContainer[playerId].imageContainer.color = new(1f, 1f, 1f, 0f);
        m_charactersContainer[playerId].nameContainer.text = "";
        m_charactersContainer[playerId].border.SetActive(true);
        m_charactersContainer[playerId].borderClient.SetActive(false);
        m_charactersContainer[playerId].borderReady.SetActive(false);
        m_charactersContainer[playerId].playerIcon.gameObject.SetActive(false);
        m_charactersContainer[playerId].playerIcon.color = m_playerColor;
        m_charactersContainer[playerId].backgroundWeapon.SetActive(false);
        m_charactersContainer[playerId].backgroundWeaponReady.SetActive(false);
        m_charactersContainer[playerId].backgroundClientWeaponReady.SetActive(false);
        m_charactersContainer[playerId].waitingText.SetActive(true);
    }

    public void SetCharacterColor(int playerId, int characterSelected)
    {
        if (characterData[characterSelected].isSelected)
        {
            m_charactersContainer[playerId].imageContainer.color = k_selectedColor;
            m_charactersContainer[playerId].nameContainer.color = k_selectedColor;
        }
        else
        {
            m_charactersContainer[playerId].imageContainer.color = Color.white;
            m_charactersContainer[playerId].nameContainer.color = Color.white;
        }
    }

    public void SetCharacterUI(int playerId, int characterSelected)
    {
        m_charactersContainer[playerId].imageContainer.sprite = characterData[characterSelected].characterSprite;

        m_charactersContainer[playerId].backgroundWeaponImage.sprite = characterData[characterSelected].weaponSprite;

        m_charactersContainer[playerId].backgroundWeaponReadyImage.sprite = characterData[
            characterSelected
        ].weaponSprite;

        m_charactersContainer[playerId].backgroundClientWeaponReadyImage.sprite = characterData[
            characterSelected
        ].weaponSprite;

        m_charactersContainer[playerId].nameContainer.text = characterData[characterSelected].characterName;

        SetCharacterColor(playerId, characterSelected);
    }

    #endregion


    /////////////////////////////////////////////////////////////////

    #region Network RPC

    [ClientRpc]
    private void StartGameClientRpc()
    {
        LoadingFadeEffect.Instance.FadeAll();
    }

    [ClientRpc]
    private void UpdateCountdownClientRpc(int countdown, float timer)
    {
        if (m_countdownContainer != null)
        {
            m_countdownContainer.gameObject.SetActive(true);
        }

        if (m_countdownText != null)
        {
            m_countdownText.text = countdown.ToString();
        }
    }

    [ClientRpc]
    private void PlayCountdownVoiceoverClientRpc(int countdownNumber)
    {
        if (voiceoverConfig != null && AudioService.Instance != null)
        {
            AudioClip countdownClip = voiceoverConfig.GetCountdownClip(countdownNumber);
            if (countdownClip != null)
            {
                AudioHelper.PlaySound(countdownClip, AudioCategory.UI, voiceoverConfig.voiceoverVolume);
            }
        }
    }

    [ClientRpc]
    private void PlayReadyVoiceoverClientRpc()
    {
        if (voiceoverConfig != null && voiceoverConfig.ready != null && AudioService.Instance != null)
        {
            AudioHelper.PlaySound(voiceoverConfig.ready, AudioCategory.UI, voiceoverConfig.voiceoverVolume);
        }
    }

    [ClientRpc]
    private void PlayGameStartVoiceoverClientRpc()
    {
        if (voiceoverConfig != null && voiceoverConfig.gameStart != null && AudioService.Instance != null)
        {
            AudioHelper.PlaySound(voiceoverConfig.gameStart, AudioCategory.UI, voiceoverConfig.voiceoverVolume);
        }
    }

    [ClientRpc]
    private void UpdatePlayerStateClientRpc(ulong clientId, int stateIndex, ConnectionState state)
    {
        if (IsServer)
        {
            return;
        }

        m_playerStates[stateIndex].playerState = state;
        m_playerStates[stateIndex].clientId = clientId;
    }

    [ClientRpc]
    private void PlayerConnectsClientRpc(
        ulong clientId,
        int stateIndex,
        ConnectionState state,
        NetworkObjectReference player
    )
    {
        if (IsServer)
        {
            return;
        }

        if (state != ConnectionState.disconnected)
        {
            m_playerStates[stateIndex].playerState = state;
            m_playerStates[stateIndex].clientId = clientId;

            if (player.TryGet(out NetworkObject playerObject))
            {
                m_playerStates[stateIndex].playerObject = playerObject.GetComponent<PlayerCharSelection>();
            }
        }
    }

    [ClientRpc]
    private void PlayerReadyClientRpc(ulong clientId, int playerId, int characterSelected)
    {
        // Server already set characterData in PlayerReady(), only update on clients
        if (!IsServer)
        {
            characterData[characterSelected].isSelected = true;
            characterData[characterSelected].clientId = clientId;
            characterData[characterSelected].playerId = playerId;
        }
        m_playerStates[playerId].playerState = ConnectionState.ready;

        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            m_charactersContainer[playerId].backgroundClientWeaponReady.SetActive(true);
            m_charactersContainer[playerId].backgroundWeapon.SetActive(false);
        }
        else
        {
            m_charactersContainer[playerId].border.SetActive(false);
            m_charactersContainer[playerId].borderReady.SetActive(true);
            m_charactersContainer[playerId].backgroundWeapon.SetActive(false);
            m_charactersContainer[playerId].backgroundWeaponReady.SetActive(true);
        }

        for (int i = 0; i < m_playerStates.Length; i++)
        {
            if (m_playerStates[i].playerState == ConnectionState.connected)
            {
                if (m_playerStates[i].playerObject.CharSelected == characterSelected)
                {
                    SetCharacterColor(i, characterSelected);
                }
            }
        }
    }

    [ClientRpc]
    private void PlayerNotReadyClientRpc(ulong clientId, int playerId, int characterSelected)
    {
        characterData[characterSelected].isSelected = false;
        characterData[characterSelected].clientId = 0UL;
        characterData[characterSelected].playerId = -1;

        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            m_charactersContainer[playerId].borderClient.SetActive(true);
            m_charactersContainer[playerId].backgroundClientWeaponReady.SetActive(false);
            m_charactersContainer[playerId].backgroundWeapon.SetActive(true);
        }
        else
        {
            m_charactersContainer[playerId].border.SetActive(true);
            m_charactersContainer[playerId].borderReady.SetActive(false);
            m_charactersContainer[playerId].borderClient.SetActive(false);
            m_charactersContainer[playerId].backgroundWeapon.SetActive(true);
            m_charactersContainer[playerId].backgroundWeaponReady.SetActive(false);
        }

        for (int i = 0; i < m_playerStates.Length; i++)
        {
            if (m_playerStates[i].playerState == ConnectionState.connected)
            {
                if (m_playerStates[i].playerObject.CharSelected == characterSelected)
                {
                    SetCharacterColor(i, characterSelected);
                }
            }
        }
    }

    [ClientRpc]
    public void PlayerDisconnectClientRpc(int playerId)
    {
        SetNonPlayableChar(playerId);

        RemoveSelectedStates();

        m_playerStates[playerId].playerState = ConnectionState.disconnected;
    }

    #endregion

    /////////////////////////////////////////////////////////////////
    #region Audio helpers


    public void PlayButtonClickSound()
    {
        if (uiSoundConfig != null && uiSoundConfig.buttonClick != null && AudioService.Instance != null)
        {
            AudioHelper.PlaySound(uiSoundConfig.buttonClick, AudioCategory.UI, uiSoundConfig.uiSoundVolume);
        }
        else if (m_confirmClip != null && AudioService.Instance != null)
        {
            AudioHelper.PlaySound(m_confirmClip, AudioCategory.UI);
        }
    }

    public void PlayButtonCancelSound()
    {
        if (uiSoundConfig != null && uiSoundConfig.buttonCancel != null && AudioService.Instance != null)
        {
            AudioHelper.PlaySound(uiSoundConfig.buttonCancel, AudioCategory.UI, uiSoundConfig.uiSoundVolume);
        }
        else if (m_cancelClip != null && AudioService.Instance != null)
        {
            AudioHelper.PlaySound(m_cancelClip, AudioCategory.UI);
        }
    }

    #endregion
    
    /////////////////////////////////////////////////////////////////
    
    #region Exit Room
    
    /// <summary>
    /// Called when exit room button is clicked
    /// </summary>
    public void OnExitRoomClicked()
    {
        // Prevent multiple clicks
        if (_isShuttingDown)
        {
            return;
        }
        
        _isShuttingDown = true;
        
        // Stop the countdown timer immediately
        m_isTimerOn = false;
        if (m_countdownContainer != null)
        {
            m_countdownContainer.SetActive(false);
        }
        
        PlayButtonCancelSound();
        
        if (IsServer)
        {
            // Unsubscribe to prevent callback during shutdown
            NetworkManager.Singleton.OnClientDisconnectCallback -= PlayerDisconnects;
            
            // Host is exiting - notify all clients to return to menu, then shutdown
            if (IsSpawned)
            {
                HostExitingRoomClientRpc();
            }
            
            // Give clients a moment to receive the RPC before shutting down
            StartCoroutine(HostShutdownCoroutine());
        }
        else
        {
            // Client is exiting - just disconnect this client
            ClientExitRoom();
        }
    }
    
    private System.Collections.IEnumerator HostShutdownCoroutine()
    {
        yield return new WaitForSeconds(0.3f);
        
        // Shutdown network
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.Shutdown();
        }
        
        // Return to menu
        yield return new WaitForSeconds(0.2f);
        LoadingSceneManager.Instance.LoadScene(SceneName.Menu, false);
    }
    
    private void ClientExitRoom()
    {
        // Notify server that we're leaving
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsConnectedClient)
        {
            RequestExitRoomServerRpc(NetworkManager.Singleton.LocalClientId);
        }
        
        // Disconnect from server
        StartCoroutine(ClientShutdownCoroutine());
    }
    
    private System.Collections.IEnumerator ClientShutdownCoroutine()
    {
        yield return new WaitForSeconds(0.2f);
        
        // Shutdown network connection
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.Shutdown();
        }
        
        // Return to menu
        yield return new WaitForSeconds(0.2f);
        LoadingSceneManager.Instance.LoadScene(SceneName.Menu, false);
    }
    
    /// <summary>
    /// Client requests to exit room - server handles cleanup
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    private void RequestExitRoomServerRpc(ulong clientId)
    {
        // Server will handle this client's disconnect
        // The OnClientDisconnectCallback will handle cleanup
        NetworkManager.Singleton.DisconnectClient(clientId);
    }
    
    /// <summary>
    /// Notifies all clients that host is leaving - everyone must return to menu
    /// </summary>
    [ClientRpc]
    private void HostExitingRoomClientRpc()
    {
        if (IsServer)
        {
            return; // Host handles its own exit
        }
        
        // Client: Host is leaving, we need to return to menu
        Debug.Log("[CharacterSelectionManager] Host is leaving the room, returning to menu...");
        
        StartCoroutine(ClientReturnToMenuCoroutine());
    }
    
    private System.Collections.IEnumerator ClientReturnToMenuCoroutine()
    {
        yield return new WaitForSeconds(0.1f);
        
        // Shutdown network
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.Shutdown();
        }
        
        yield return new WaitForSeconds(0.2f);
        
        // Return to menu
        LoadingSceneManager.Instance.LoadScene(SceneName.Menu, false);
    }
    
    #endregion
}
