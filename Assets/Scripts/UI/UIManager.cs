using System;
using System.Collections.Generic;
using System.Linq;
using Mirror;
using TMPro;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

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

    [Header("Map/Character Resources")]
    [SerializeField]
    private MapDefinitionSO[] maps;

    [SerializeField]
    private CharacterDefinitionSO[] characters;

    private MapDefinitionSO selectedMap;
    private CharacterDefinitionSO selectedCharacter;
    private readonly Dictionary<int, GameObject> playerListItems = new();

    // LOBBY Buttons
    [Header("Lobby")]
    [SerializeField]
    private Button playOfflineBtn;

    [SerializeField]
    private Button hostGameBtn;

    [SerializeField]
    private Button joinGameBtn;

    [SerializeField]
    private Button lobbyBackBtn;

    // MAP
    [Header("Map Selection")]
    [SerializeField]
    private Transform mapGridParent;

    [SerializeField]
    private GameObject mapCardPrefab;

    [SerializeField]
    private Button mapBackBtn;

    // CHARACTER
    [Header("Character Selection")]
    [SerializeField]
    private Transform characterGridParent;

    [SerializeField]
    private GameObject characterCardPrefab;

    [SerializeField]
    private Button characterBackBtn;

    // ROOM
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

    // ROOM LIST
    [Header("Room List")]
    [SerializeField]
    private Transform roomListParent;

    [SerializeField]
    private GameObject roomListItemPrefab;

    [SerializeField]
    private Button refreshRoomsBtn;

    [SerializeField]
    private Button roomListBackBtn;

    // COUNTDOWN
    [Header("Countdown")]
    [SerializeField]
    private TextMeshProUGUI countdownText;

    // runtime state
    private bool isReady = false;
    private LobbyController lobbyController;
    private List<GameObject> activeRoomListItems = new();

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
    }

    private void Start()
    {
        lobbyController = LobbyController.Instance;

        if (lobbyController == null)
        {
            Debug.LogError("LobbyController not found in scene. Please add it.");
            return;
        }

        playOfflineBtn.onClick.AddListener(OnPlayOffline);
        hostGameBtn.onClick.AddListener(OnHostGame);
        joinGameBtn.onClick.AddListener(OnJoinGame);
        lobbyBackBtn.onClick.AddListener(OnLobbyBack);

        mapBackBtn.onClick.AddListener(OnMapBack);
        characterBackBtn.onClick.AddListener(OnCharacterBack);

        refreshRoomsBtn.onClick.AddListener(OnRefreshRooms);
        roomListBackBtn.onClick.AddListener(OnRoomListBack);

        readyBtn.onClick.AddListener(OnReadyToggle);
        startMatchBtn.onClick.AddListener(OnStartMatchClicked);
        leaveRoomBtn.onClick.AddListener(OnLeaveRoom);

        // subscribe lobby events
        lobbyController.OnLobbyCreated += OnLobbyCreated;
        lobbyController.OnLobbyJoined += OnLobbyJoined;
        lobbyController.OnLobbyListUpdated += OnLobbyListUpdated;
        lobbyController.OnLobbyUpdated += OnLobbyUpdated;
        lobbyController.OnLobbyLeft += OnLobbyLeft;
        lobbyController.OnLobbyDeleted += OnLobbyDeleted;
        lobbyController.OnError += msg => Debug.LogWarning("Lobby error: " + msg);

        // initial UI
        ShowPanel(lobbyPanel);

        PopulateMapGrid();
        PopulateCharacterGrid();
    }

    #region UI Panel Helpers
    private void ShowPanel(GameObject panel)
    {
        lobbyPanel.SetActive(panel == lobbyPanel);
        mapSelectionPanel.SetActive(panel == mapSelectionPanel);
        characterSelectionPanel.SetActive(panel == characterSelectionPanel);
        roomPanel.SetActive(panel == roomPanel);
        roomListPanel.SetActive(panel == roomListPanel);
        countdownPanel.SetActive(panel == countdownPanel);
    }
    #endregion

    #region Map / Character Grid Population
    private void PopulateMapGrid()
    {
        foreach (Transform t in mapGridParent)
        {
            Destroy(t.gameObject);
        }
        if (mapCardPrefab == null)
        {
            return;
        }

        foreach (var map in maps)
        {
            var go = Instantiate(mapCardPrefab, mapGridParent);
            var m = go.GetComponent<MapCardUI>();
            m.Setup(map);
        }
    }

    private void PopulateCharacterGrid()
    {
        foreach (Transform t in characterGridParent)
        {
            Destroy(t.gameObject);
        }
        if (characterCardPrefab == null)
        {
            return;
        }

        foreach (var c in characters)
        {
            var go = Instantiate(characterCardPrefab, characterGridParent);
            var cc = go.GetComponent<CharacterCardUI>();
            cc.Setup(c);
        }
    }

    public void OnMapSelected(MapDefinitionSO map)
    {
        selectedMap = map;
        ShowPanel(characterSelectionPanel);
    }

    public async void OnCharacterSelected(CharacterDefinitionSO character)
    {
        selectedCharacter = character;

        //  inside a room (joined/hosted) => update player data
        if (roomPanel.activeSelf)
        {
            await lobbyController.SetSelectedCharacterAsync(character.assetId);
            RefreshPlayerListUIFromLobby(lobbyController.GetCurrentLobby());
            return;
        }

        //  host room: create lobby and start host
        if (string.IsNullOrEmpty(selectedMap?.sceneName))
        {
            Debug.LogError($"selected Map does not have sceneName");
        }

        if (lobbyController.GetCurrentLobby() == null && LobbyModeTracker.IsPendingHost)
        {
            string lobbyName = $"Lobby_{PlayerLocalId()}";

            int maxPlayers = selectedMap != null ? Mathf.Clamp(selectedMap.maxPlayers, 2, 8) : 4;

            await lobbyController.CreateLobbyAsync(lobbyName, maxPlayers, "Default", selectedMap?.assetId);

            await lobbyController.SetSelectedCharacterAsync(selectedCharacter.assetId);

            // start Mirror host
            NetworkSessionController.Instance.StartHost();

            LobbyModeTracker.IsPendingHost = false;
            return;
        }

        // Join room
        if (lobbyController.GetCurrentLobby() != null)
        {
            await lobbyController.SetSelectedCharacterAsync(selectedCharacter.assetId);

            RefreshPlayerListUIFromLobby(lobbyController.GetCurrentLobby());

            ShowPanel(roomPanel);
            return;
        }

        // Handle offline mode: load game scene directly
        if (LobbyModeTracker.IsOffline)
        {
            if (!string.IsNullOrEmpty(selectedMap?.sceneName))
            {
                Debug.Log($"[Offline] Loading scene {selectedMap.sceneName}");

                LoadingSceneController.LoadScene(selectedMap.sceneName);
                return;
            }
        }

        // default: go back to lobby UI
        ShowPanel(lobbyPanel);
    }
    #endregion

    #region Button Callbacks

    private void OnPlayOffline()
    {
        LobbyModeTracker.Clear();
        LobbyModeTracker.IsOffline = true;
        ShowPanel(mapSelectionPanel);
    }

    private void OnHostGame()
    {
        LobbyModeTracker.Clear();
        LobbyModeTracker.IsPendingHost = true;
        ShowPanel(mapSelectionPanel);
    }

    private void OnJoinGame()
    {
        LobbyModeTracker.Clear();
        LobbyModeTracker.IsPendingJoin = true;
        ShowPanel(roomListPanel);
        OnRefreshRooms();
    }

    private void OnLobbyBack() => ShowPanel(lobbyPanel);

    private void OnMapBack() => ShowPanel(lobbyPanel);

    private void OnCharacterBack()
    {
        if (LobbyModeTracker.IsPendingHost || LobbyModeTracker.IsPendingJoin)
        {
            ShowPanel(mapSelectionPanel);
        }
        else
        {
            ShowPanel(lobbyPanel);
        }
    }

    private void OnRoomListBack() => ShowPanel(lobbyPanel);
    #endregion

    #region Room List UI

    private async void OnRefreshRooms()
    {
        foreach (var go in activeRoomListItems)
        {
            Destroy(go);
        }
        activeRoomListItems.Clear();

        await lobbyController.ListLobbiesAsync();
    }

    private void OnLobbyListUpdated(List<RoomInfo> list)
    {
        UnityMainThreadDispatcher
            .Instance()
            .Enqueue(() =>
            {
                foreach (var go in activeRoomListItems)
                {
                    Destroy(go);
                }
                activeRoomListItems.Clear();

                foreach (var r in list)
                {
                    var inst = Instantiate(roomListItemPrefab, roomListParent);
                    var ui = inst.GetComponent<RoomListItemUI>();
                    ui.Setup(r);
                    activeRoomListItems.Add(inst);
                }
            });
    }
    #endregion

    #region Lobby / Room callbacks

    private void OnLobbyCreated(Lobby lobby)
    {
        UnityMainThreadDispatcher
            .Instance()
            .Enqueue(() =>
            {
                ShowPanel(roomPanel);

                NetworkSessionController.Instance.StartHost();

                RefreshPlayerListUIFromLobby(lobby);
            });
    }

    private void OnLobbyJoined(Lobby lobby)
    {
        UnityMainThreadDispatcher
            .Instance()
            .Enqueue(() =>
            {
                ShowPanel(characterSelectionPanel);
            });
    }

    private void OnLobbyUpdated(Lobby lobby)
    {
        UnityMainThreadDispatcher
            .Instance()
            .Enqueue(() =>
            {
                RefreshPlayerListUIFromLobby(lobby);

                if (lobby.Data != null && lobby.Data.TryGetValue("Started", out var started) && started.Value == "1")
                {
                    long countdownStart = 0;
                    if (lobby.Data.TryGetValue("CountdownStartTime", out var cst))
                        long.TryParse(cst.Value, out countdownStart);

                    int remaining = (int)(countdownStart - DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                    if (remaining < 0)
                        remaining = 0;

                    StartCoroutine(RunCountdownAndStart(remaining));
                }
            });
    }

    private void OnLobbyLeft()
    {
        UnityMainThreadDispatcher
            .Instance()
            .Enqueue(() =>
            {
                ShowPanel(lobbyPanel);

                // stop mirror client/host if running
                NetworkSessionController.Instance.StopNetwork();
            });
    }

    private void OnLobbyDeleted()
    {
        UnityMainThreadDispatcher
            .Instance()
            .Enqueue(() =>
            {
                ShowPanel(lobbyPanel);

                NetworkSessionController.Instance.StopNetwork();
            });
    }
    #endregion

    #region Player List (room) UI updates

    private void RefreshPlayerListUIFromLobby(Lobby lobby)
    {
        UnityMainThreadDispatcher
            .Instance()
            .Enqueue(() =>
            {
                // clear existing items
                foreach (Transform t in playerListParent)
                {
                    Destroy(t.gameObject);
                }

                playerListItems.Clear();

                if (lobby == null || lobby.Players == null)
                {
                    return;
                }

                int readyCount = 0;
                foreach (var p in lobby.Players)
                {
                    var inst = Instantiate(playerListItemPrefab, playerListParent);
                    var ui = inst.GetComponent<PlayerListItemUI>();

                    string playerId = p.Id;

                    string playerName =
                        p.Data != null && p.Data.TryGetValue("PlayerName", out var nameData)
                            ? nameData.Value
                            : playerId;

                    string charName = "";
                    if (
                        p.Data != null
                        && p.Data.TryGetValue("Character", out var charData)
                        && !string.IsNullOrEmpty(charData.Value)
                    )
                    {
                        CharacterDefinitionSO so = AssetDatabaseNetwork.GetAsset<CharacterDefinitionSO>(charData.Value);

                        charName = so != null ? so.characterName : "Unknown";
                    }

                    bool isReady = p.Data != null && p.Data.TryGetValue("IsReady", out var rpd) && rpd.Value == "1";

                    ui.Setup(playerName, charName, isReady);

                    playerListItems[playerId.GetHashCode()] = inst;

                    if (isReady)
                    {
                        readyCount++;
                    }
                }
                readyCountText.text = $"{readyCount}/{lobby.MaxPlayers} ready";

                // enable start button only for host
                startMatchBtn.gameObject.SetActive(lobbyController.IsHost);
            });
    }
    #endregion

    #region Ready / Start / Leave
    private async void OnReadyToggle()
    {
        isReady = !isReady;

        await lobbyController.SetPlayerReadyAsync(isReady);
        readyBtn.GetComponentInChildren<TextMeshProUGUI>().text = isReady ? "Unready" : "Ready";
    }

    private async void OnStartMatchClicked()
    {
        var lobby = lobbyController.GetCurrentLobby();
        if (lobby == null)
            return;

        //  Check if all players ready
        bool allReady = lobby.Players.All(p =>
            p.Data != null && p.Data.TryGetValue("IsReady", out var rpd) && rpd.Value == "1"
        );

        if (!allReady)
        {
            Debug.LogWarning("Not all players are ready — cannot start match!");
            return;
        }

        //  Host only allowed to start
        if (!lobbyController.IsHost)
        {
            Debug.LogWarning("Only host can start the match!");
            return;
        }

        string sceneToLoad = selectedMap != null ? selectedMap.sceneName : "";
        await lobbyController.StartMatchFromHostAsync(sceneToLoad);
    }

    private async void OnLeaveRoom()
    {
        await lobbyController.LeaveLobbyAsync();
    }

    #endregion

    #region Countdown Coroutine

    private System.Collections.IEnumerator RunCountdownAndStart(int seconds)
    {
        countdownPanel.SetActive(true);
        for (int i = seconds; i > 0; i--)
        {
            countdownText.text = i.ToString();
            yield return new WaitForSeconds(1f);
        }

        countdownText.text = "GO!";
        yield return new WaitForSeconds(0.5f);
        countdownPanel.SetActive(false);

        // If the server, instruct server to change scene and spawn players
        if (NetworkServer.active)
        {
            var lobby = lobbyController.GetCurrentLobby();
            string sceneName = selectedMap?.sceneName;

            if (
                lobby != null
                && lobby.Data != null
                && lobby.Data.TryGetValue("SelectedMap", out var d)
                && !string.IsNullOrEmpty(d.Value)
            )
            {
                // try to resolve assetId -> MapDefinitionSO for scene name
                MapDefinitionSO foundMap = AssetDatabaseNetwork.GetAsset<MapDefinitionSO>(d.Value);

                if (foundMap != null)
                {
                    sceneName = foundMap.sceneName;
                }
            }

            NetworkSessionController.Instance.StartServerMatch(sceneName);
        }
        else
        {
            // client will be moved by Mirror when server changes scene
        }
    }
    #endregion

    // small helper to find some unique local id for lobby name
    private string PlayerLocalId()
    {
        return System.Guid.NewGuid().ToString().Substring(0, 6);
    }
}

public static class LobbyModeTracker
{
    public static bool IsPendingHost = false;
    public static bool IsPendingJoin = false;
    public static bool IsOffline = false;

    public static void Clear()
    {
        IsPendingHost = IsPendingJoin = IsOffline = false;
    }
}
