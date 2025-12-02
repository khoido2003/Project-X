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

    [Header("Audio clip")]
    [SerializeField]
    private AudioClip m_confirmClip;

    [SerializeField]
    private AudioClip m_cancelClip;

    private bool m_isTimerOn;
    private float m_timer;

    private readonly Color k_selectedColor = new Color32(74, 74, 74, 255);

    private void Start()
    {
        m_countdownContainer.gameObject.SetActive(false);
        m_timer = m_timeToStartGame;
        RemoveSelectedStates();
    }

    private void Update()
    {
        if (!IsServer)
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
        m_countdownText.text = Mathf.FloorToInt(m_timer).ToString();

        if (m_timer <= 0f)
        {
            m_isTimerOn = false;
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
        if (!ClientConnection.Instance.IsExtraClient(clientId))
        {
            return;
        }

        if (clientId == 0)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback -= PlayerDisconnects;
            NetworkManager.Singleton.Shutdown();
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

        m_countdownContainer.gameObject.SetActive(true);
        m_countdownText.text = Mathf.FloorToInt(m_timer).ToString();
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
        characterData[characterSelected].isSelected = true;
        characterData[characterSelected].clientId = clientId;
        characterData[characterSelected].playerId = playerId;
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
}
