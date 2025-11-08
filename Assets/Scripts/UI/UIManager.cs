using System;
using System.Collections.Generic;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum UIManagerState
{
    Lobby,
    MapSelection,
    CharacterSelection,
    Room,
    RoomList,
    Countdown,
    InGame,
}

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    // ==========================
    //  COMMON SETUP
    // ==========================
    [Header("Panels")]
    [SerializeField]
    private GameObject lobbyPanel;

    [SerializeField]
    private GameObject mapSelectionPanel;

    [SerializeField]
    private GameObject characterSelectionPanel;

    [SerializeField]
    private GameObject roomPanel;

    [SerializeField]
    private GameObject roomListPanel;

    [SerializeField]
    private GameObject countdownPanel;

    private MapDefinitionSO selectedMap;
    private CharacterDefinitionSO selectedCharacter;
    private bool isHostMode;
    private readonly Dictionary<int, GameObject> playerListItems = new();

    // ==========================
    //  LOBBY PANEL
    // ==========================
    [Header("Lobby")]
    [SerializeField]
    private Button playOfflineBtn;

    [SerializeField]
    private Button hostGameBtn;

    [SerializeField]
    private Button joinGameBtn;

    // [SerializeField]
    // private InputField roomNameInput;

    [SerializeField]
    private Button lobbyBackBtn;

    // ==========================
    //  MAP SELECTION PANEL
    // ==========================
    [Header("Map Selection")]
    [SerializeField]
    private Transform mapGridParent;

    [SerializeField]
    private GameObject mapCardPrefab;

    [SerializeField]
    private Button mapBackBtn;

    // ==========================
    //  CHARACTER SELECTION PANEL
    // ==========================
    [Header("Character Selection")]
    [SerializeField]
    private Transform characterGridParent;

    [SerializeField]
    private GameObject characterCardPrefab;

    [SerializeField]
    private Button characterBackBtn;

    // ==========================
    //  ROOM PANEL
    // ==========================
    [Header("Room")]
    [SerializeField]
    private Button readyBtn;

    [SerializeField]
    private Button startMatchBtn;

    [SerializeField]
    private Button leaveRoomBtn;

    [SerializeField]
    private TextMeshProUGUI readyCountText;

    [SerializeField]
    private Transform playerListParent;

    [SerializeField]
    private GameObject playerListItemPrefab;

    // ==========================
    //  COUNTDOWN PANEL
    // ==========================
    [Header("Countdown")]
    [SerializeField]
    private TextMeshProUGUI countdownText;

    // ==========================
    // ROOM LIST PANEL
    // ==========================
    [Header("Room List")]
    [SerializeField]
    private Transform roomListParent;

    [SerializeField]
    private GameObject roomListItemPrefab;

    [SerializeField]
    private Button refreshRoomsBtn;

    [SerializeField]
    private Button roomListBackBtn;

    ////////////////////////////////////////////////

    private void Awake()
    {
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        SetupLobbyPanel();
        SetupMapSelectionPanel();
        SetupCharacterSelectionPanel();
        SetupRoomPanel();
        SetupRoomListPanel();
        SetupCountdownPanel();

        // Register events
        if (LANDiscovery.Instance != null)
        {
            LANDiscovery.Instance.OnRoomsUpdated += UpdateRoomList;
        }

        if (GameSession.Instance != null)
        {
            GameSession.Instance.OnPlayerChoiceChanged += OnPlayerChoiceChanged;
            GameSession.Instance.OnReadyCountChanged += UpdateReadyCount;
        }

        SetState(UIManagerState.Lobby);
        StartCoroutine(SubscribeToGameSessionWhenReady());
    }

    private void OnDestroy()
    {
        if (LANDiscovery.Instance != null)
        {
            LANDiscovery.Instance.OnRoomsUpdated -= UpdateRoomList;
        }

        if (GameSession.Instance != null)
        {
            GameSession.Instance.OnPlayerChoiceChanged -= OnPlayerChoiceChanged;
        }
    }

    private System.Collections.IEnumerator SubscribeToGameSessionWhenReady()
    {
        // If already present, subscribe immediately
        if (GameSession.Instance != null)
        {
            SubscribeToGameSession();
            yield break;
        }

        // Wait until GameSession exists (host spawn or client arrival)
        yield return new WaitUntil(() => GameSession.Instance != null);

        // Then subscribe
        SubscribeToGameSession();
    }

    private void SubscribeToGameSession()
    {
        if (GameSession.Instance == null)
            return;

        // Unsubscribe first to avoid double subscription (safety)
        GameSession.Instance.OnPlayerChoiceChanged -= OnPlayerChoiceChanged;
        GameSession.Instance.OnReadyCountChanged -= UpdateReadyCount;

        GameSession.Instance.OnPlayerChoiceChanged += OnPlayerChoiceChanged;
        GameSession.Instance.OnReadyCountChanged += UpdateReadyCount;

        // Make sure the room UI reflects current state:
        UpdateReadyCount(GameSession.Instance.readyCount);
    }

    private void SetActivePanel(GameObject active)
    {
        lobbyPanel.SetActive(false);
        mapSelectionPanel.SetActive(false);
        characterSelectionPanel.SetActive(false);
        roomPanel.SetActive(false);
        roomListPanel.SetActive(false);
        countdownPanel.SetActive(false);

        if (active != null)
        {
            active.SetActive(true);
        }
    }

    public void SetState(UIManagerState state, bool isHost = false)
    {
        isHostMode = isHost;
        switch (state)
        {
            case UIManagerState.Lobby:
                SetActivePanel(lobbyPanel);
                break;
            case UIManagerState.MapSelection:
                SetActivePanel(mapSelectionPanel);
                PopulateMapGrid();
                break;
            case UIManagerState.CharacterSelection:
                SetActivePanel(characterSelectionPanel);
                PopulateCharacterGrid();
                break;
            case UIManagerState.Room:
                SetActivePanel(roomPanel);
                break;
            case UIManagerState.RoomList:
                SetActivePanel(roomListPanel);
                LANDiscovery.Instance?.rooms.Clear();
                break;
            case UIManagerState.Countdown:
                SetActivePanel(countdownPanel);
                break;
        }
    }

    // ==========================
    //  LOBBY PANEL
    // ==========================

    private void SetupLobbyPanel()
    {
        playOfflineBtn.onClick.AddListener(() =>
        {
            isHostMode = false;
            GameFlowService.Instance?.SetOfflineMode(true);
            SetState(UIManagerState.MapSelection);
        });

        hostGameBtn.onClick.AddListener(OnHostGame);
        joinGameBtn.onClick.AddListener(() => SetState(UIManagerState.RoomList));
        lobbyBackBtn.onClick.AddListener(() => Application.Quit());
    }

    private void OnHostGame()
    {
        isHostMode = true;
        GameFlowService.Instance?.SetHostMode(true);
        SetState(UIManagerState.MapSelection);
    }

    // ==========================
    //  MAP SELECTION PANEL
    // ==========================

    private void SetupMapSelectionPanel()
    {
        mapBackBtn.onClick.AddListener(() => SetState(UIManagerState.Lobby));
    }

    private void PopulateMapGrid()
    {
        ClearGrid(mapGridParent);
        foreach (var map in AssetDatabaseNetwork.GetAllAssets<MapDefinitionSO>())
        {
            var cardGO = Instantiate(mapCardPrefab, mapGridParent);
            var card = cardGO.GetComponent<MapCardUI>();
            card.Setup(map);
        }
    }

    public void OnMapSelected(MapDefinitionSO map)
    {
        selectedMap = map;
        if (isHostMode && GameSession.Instance != null)
        {
            GameSession.Instance.CmdSetMap(map.assetId);
        }
        SetState(UIManagerState.CharacterSelection);
    }

    // ==========================
    //  CHARACTER SELECTION PANEL
    // ==========================

    private void SetupCharacterSelectionPanel()
    {
        characterBackBtn.onClick.AddListener(() => SetState(UIManagerState.MapSelection));
    }

    private void PopulateCharacterGrid()
    {
        ClearGrid(characterGridParent);
        foreach (var ch in AssetDatabaseNetwork.GetAllAssets<CharacterDefinitionSO>())
        {
            var cardGO = Instantiate(characterCardPrefab, characterGridParent);
            var card = cardGO.GetComponent<CharacterCardUI>();
            card.Setup(ch);
        }
    }

    public void OnCharacterSelected(CharacterDefinitionSO character)
    {
        selectedCharacter = character;

        // === OFFLINE FLOW ===
        if (GameFlowService.Instance != null && GameFlowService.Instance.IsOffline)
        {
            GameFlowService.Instance.StartOffline(selectedMap, selectedCharacter);
            return;
        }

        // === HOST FLOW ===
        if (isHostMode && !NetworkServer.active && !NetworkClient.active)
        {
            Debug.Log("Character select here should be called!");

            string roomName = RoomNameService.GetNextRoomName();
            GameFlowService.Instance?.StartHost(roomName);

            StartCoroutine(WaitThenChooseCharacter(character.assetId));
        }
        // === CLIENT FLOW ===
        else if (NetworkClient.active && GameSession.Instance != null)
        {
            GameSession.Instance.CmdChooseCharacter(character.assetId);
        }

        SetState(UIManagerState.Room);
    }

    private System.Collections.IEnumerator WaitThenChooseCharacter(string id)
    {
        yield return new WaitUntil(() => GameSession.Instance != null && NetworkServer.active);
        GameSession.Instance.CmdChooseCharacter(id);
    }

    // ==========================
    //  ROOM PANEL
    // ==========================

    private void SetupRoomPanel()
    {
        readyBtn.onClick.AddListener(() => GameSession.Instance?.CmdToggleReady());
        startMatchBtn.onClick.AddListener(() => GameSession.Instance?.StartMatch());
        leaveRoomBtn.onClick.AddListener(OnLeaveRoom);
    }

    private void OnLeaveRoom()
    {
        if (NetworkClient.active)
        {
            NetworkManager.singleton.StopClient();
        }
        if (NetworkServer.active)
        {
            NetworkManager.singleton.StopHost();
        }
        SetState(UIManagerState.RoomList);
    }

    private void OnPlayerChoiceChanged(object sender, GameSession.OnPlayerChoiceChangedArgs e)
    {
        UpdatePlayerListItem(e.playerId, e.displayName);
    }

    private void UpdatePlayerListItem(int playerId, string characterName)
    {
        if (playerListItems.TryGetValue(playerId, out var item))
        {
            item.GetComponentInChildren<TextMeshProUGUI>().text = $"P{playerId}: {characterName}";
        }
        else
        {
            var newItem = Instantiate(playerListItemPrefab, playerListParent);
            newItem.GetComponentInChildren<TextMeshProUGUI>().text = $"P{playerId}: {characterName}";
            playerListItems[playerId] = newItem;
        }
    }

    private void UpdateReadyCount(int count)
    {
        int max = GameSession.Instance?.maxPlayers ?? -1;
        bool host = GameFlowService.Instance != null && GameFlowService.Instance.IsHost;
        readyCountText.text = $"{count}/{max} Ready";
        startMatchBtn.interactable = host && count == max;
    }

    // ==========================
    // ROOM LIST PANEL
    // ==========================

    private void SetupRoomListPanel()
    {
        refreshRoomsBtn.onClick.AddListener(() => LANDiscovery.Instance?.StartDiscovery());
        roomListBackBtn.onClick.AddListener(() => SetState(UIManagerState.Lobby));
    }

    private void UpdateRoomList()
    {
        ClearGrid(roomListParent);
        foreach (var room in LANDiscovery.Instance.rooms)
        {
            var itemGO = Instantiate(roomListItemPrefab, roomListParent);
            itemGO.GetComponent<RoomListItemUI>().Setup(room);
        }
    }

    // ==========================
    //  COUNTDOWN PANEL
    // ==========================

    private void SetupCountdownPanel()
    {
        // Ensure all input/UI is blocked while counting down
        if (countdownPanel != null)
        {
            // Add a CanvasGroup if not already present
            var cg = countdownPanel.GetComponent<CanvasGroup>();
            if (cg == null)
            {
                cg = countdownPanel.AddComponent<CanvasGroup>();
            }
            cg.interactable = false;
            cg.blocksRaycasts = true;
        }
    }

    public void ShowCountdown(float time)
    {
        SetState(UIManagerState.Countdown);
        countdownText.text = ((int)time).ToString();
    }

    // ==========================
    //  HELPERS
    // ==========================
    private void ClearGrid(Transform parent)
    {
        if (parent == null)
        {
            return;
        }
        foreach (Transform child in parent)
        {
            Destroy(child.gameObject);
        }
    }
}
