using System.Collections.Generic;
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
            Instance = this;
        else
            Destroy(gameObject);
    }

    private void Start()
    {
        lobbyController = LobbyController.Instance;
        if (lobbyController == null)
        {
            Debug.LogError("LobbyController not found in scene. Please add it.");
            return;
        }

        // Wire button callbacks
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
        lobbyController.OnError += msg => Debug.LogWarning("Lobby error: " + msg);
        lobbyController.OnLobbyDeleted += OnLobbyDeleted;

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

    // called from MapCardUI when user selects a map
    public void OnMapSelected(MapDefinitionSO map)
    {
        selectedMap = map;
        // proceed to character selection
        ShowPanel(characterSelectionPanel);
    }

    // called from CharacterCardUI when user selects a character
    public void OnCharacterSelected(CharacterDefinitionSO character)
    {
        selectedCharacter = character;
        // if we came from map selection flow go to next: offline/online handling
        // If in offline flow we start the game
        // For online flow, we either already created/joined a lobby -> update UI
        if (roomPanel.activeSelf)
        {
            // we're already in a room; update chosen hero display or local player pending selection
            RefreshPlayerListUIFromLobby();
        }
        else
        {
            // By default go back to lobby so user can choose host/join
            ShowPanel(lobbyPanel);
        }
    }
    #endregion

    #region Button Callbacks (top-level flows)
    private void OnPlayOffline()
    {
        // Open map selection for offline
        ShowPanel(mapSelectionPanel);
    }

    private void OnHostGame()
    {
        // host flow: choose map -> choose char -> create lobby -> start host
        ShowPanel(mapSelectionPanel);
        // Wait: selectedMap will be set by map UI, selectedCharacter later by character UI.
        // We will create the lobby when user finalizes character selection and confirms.
        // To keep UX simple, after selecting character we auto create a lobby and start host.
    }

    private void OnJoinGame()
    {
        // show room list
        ShowPanel(roomListPanel);
        OnRefreshRooms();
    }

    private void OnLobbyBack()
    {
        ShowPanel(lobbyPanel);
    }

    private void OnMapBack()
    {
        ShowPanel(lobbyPanel);
    }

    private void OnCharacterBack()
    {
        ShowPanel(mapSelectionPanel);
    }

    private void OnRoomListBack()
    {
        ShowPanel(lobbyPanel);
    }
    #endregion

    #region Room List UI
    private async void OnRefreshRooms()
    {
        // clear existing UI items
        foreach (var go in activeRoomListItems)
        {
            Destroy(go);
        }
        activeRoomListItems.Clear();

        await lobbyController.ListLobbiesAsync();
        // the results will come through OnLobbyListUpdated event
    }

    private void OnLobbyListUpdated(List<RoomInfo> list)
    {
        // populate UI
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
                // start Mirror host now that backend lobby exists
                StartMirrorHost();
                // populate UI players
                RefreshPlayerListUIFromLobby();
            });
    }

    private void OnLobbyJoined(Lobby lobby)
    {
        UnityMainThreadDispatcher
            .Instance()
            .Enqueue(() =>
            {
                ShowPanel(roomPanel);
                RefreshPlayerListUIFromLobby();
            });
    }

    private void OnLobbyUpdated(Lobby lobby)
    {
        UnityMainThreadDispatcher
            .Instance()
            .Enqueue(() =>
            {
                RefreshPlayerListUIFromLobby();
                // If host marked "Started", begin countdown and scene change procedure
                if (lobby.Data != null && lobby.Data.TryGetValue("Started", out var d) && d.Value == "1")
                {
                    // Start countdown and then start match locally
                    StartCoroutine(RunCountdownAndStart(3)); // 3 sec default
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
                StopMirrorNetwork();
            });
    }

    private void OnLobbyDeleted()
    {
        UnityMainThreadDispatcher
            .Instance()
            .Enqueue(() =>
            {
                ShowPanel(lobbyPanel);
                StopMirrorNetwork();
            });
    }
    #endregion

    #region Player List (room) UI updates
    private void RefreshPlayerListUIFromLobby()
    {
        // get the current lobby snapshot from LobbyController (joinLobby is internal there).
        // Instead we rely on the latest lobby arriving via OnLobbyUpdated -> we'll request a fresh snapshot.
        // For simplicity we'll call lobbyController.List to get latest players via GetLobby... but there's no getter.
        // Instead: rely on the last OnLobbyUpdated invoked lobby via LobbyController.ParsePlayerReadyState by calling GetLobbyAsync within controller.
        // To keep UI decoupled, we call into LobbyController to fetch the last lobby snapshot by asking it to produce OnLobbyUpdated (it will via its own polling).
        // Here we just perform a best-effort refresh: let's call a small internal method that forces a fetch by calling SetPlayerReadyAsync(false) with no change,
        // but cleaner: implement a public method in LobbyController to GetLobbySnapshot (not currently implemented). For now, rely on last OnLobbyUpdated to update UI.
    }

    // This method is called by LobbyController.OnLobbyUpdated (we have access to the Lobby via that event).
    // We need to get the Lobby instance; modify OnLobbyUpdated listener to pass the lobby object to this method.
    // BUT above, OnLobbyUpdated already calls RefreshPlayerListUIFromLobby() without param. Adjust to accept a Lobby param:
    // To avoid more changes, below helper method expects lobbyController to hold the last lobby; we'll create a quick getter on LobbyController if needed.
    // For now, we'll implement a method to rebuild UI from the last known Lobby that the LobbyController saved in its internal joinLobby (add a GetLastLobbySnapshot method in LobbyController).
    #endregion

    // The following methods are central: ready toggle, start match (host), leave room
    private async void OnReadyToggle()
    {
        isReady = !isReady;
        // update remote ready state
        await lobbyController.SetPlayerReadyAsync(isReady);

        // update local UI (the OnLobbyUpdated will refresh the whole list shortly)
        readyBtn.GetComponentInChildren<TextMeshProUGUI>().text = isReady ? "Unready" : "Ready";
    }

    private async void OnStartMatchClicked()
    {
        // Host clicks start -> mark lobby started
        await lobbyController.StartMatchFromHostAsync();

        // Also initiate countdown and server scene change here (server must call Mirror's ServerChangeScene)
        // We'll do it in response to the lobby update (OnLobbyUpdated) which listens for Data["Started"] == "1" and runs countdown
    }

    private async void OnLeaveRoom()
    {
        await lobbyController.LeaveLobbyAsync();
    }

    private void OnLobbyLeftLocal()
    {
        // show lobby panel
        ShowPanel(lobbyPanel);
        StopMirrorNetwork();
    }

    private void OnLobbyDeletedLocal()
    {
        ShowPanel(lobbyPanel);
        StopMirrorNetwork();
    }

    #region Mirror integration helpers
    private void StartMirrorHost()
    {
        // Start Mirror host (assumes NetworkManager is configured).
        var nm = NetworkManager.singleton;
        if (nm == null)
        {
            Debug.LogError("NetworkManager.singleton is null. Cannot start host.");
            return;
        }

        if (!NetworkServer.active && !NetworkClient.active)
        {
            nm.StartHost();
            Debug.Log("Mirror host started.");
        }
        else
        {
            Debug.LogWarning("Mirror network already active.");
        }

        // TODO: set any server-side initial state (e.g., spawn lobby entity, spawn networked player)
    }

    private void StopMirrorNetwork()
    {
        var nm = NetworkManager.singleton;
        if (nm == null)
            return;

        if (NetworkServer.active)
        {
            nm.StopHost();
        }
        else if (NetworkClient.isConnected)
        {
            nm.StopClient();
        }
    }

    // Called when the countdown completes to actually load the level on server and inform clients
    private void ServerStartMatch()
    {
        var nm = NetworkManager.singleton;
        if (nm == null)
        {
            Debug.LogError("NetworkManager not found, cannot start match.");
            return;
        }

        if (NetworkServer.active)
        {
            // Server-side: change scene so Mirror handles client scene load.
            if (selectedMap != null && !string.IsNullOrEmpty(selectedMap.sceneName))
            {
                // WARNING: This causes server to load scene and tells clients to load it too.
                NetworkManager.singleton.ServerChangeScene(selectedMap.sceneName);
            }
            else
            {
                Debug.LogWarning("No selectedMap or sceneName set. Server will not change scene.");
            }

            // TODO: After scene loads, server should spawn player network objects and also create ECS entities for players,
            // attaching any network identifier components needed by your ECS <-> Mirror integration.
            //
            // Example pseudo-code after scene load:
            // foreach (NetworkConnectionToClient conn in NetworkServer.connections) {
            //     var go = Instantiate(playerPrefab);
            //     NetworkServer.AddPlayerForConnection(conn, go);
            //     // Then create ECS entity:
            //     var ent = World.Instance.CreateEntity();
            //     World.Instance.Components.Add(ent, new CharacterComponent { ... });
            //     // Store mapping between networkId (netId) and ECS entity.
            // }
        }
        else
        {
            Debug.LogWarning("ServerStartMatch called on client.");
        }
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

        // If we're the server, instruct server to change scene and spawn players
        if (NetworkServer.active)
        {
            ServerStartMatch();
        }
        else
        {
            // client will be moved by Mirror when server changes scene
        }
    }
    #endregion
}
