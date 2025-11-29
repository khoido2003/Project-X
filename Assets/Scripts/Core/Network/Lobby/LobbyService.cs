using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;

public class LobbyService : MonoBehaviour
{
    public static LobbyService Instance { get; private set; }

    public event Action<Lobby> OnLobbyCreated;
    public event Action<Lobby> OnLobbyJoined;
    public event Action<Lobby> OnLobbyUpdated;
    public event Action OnLobbyLeft;
    public event Action OnLobbyDeleted;
    public event Action<List<RoomInfo>> OnLobbyListUpdated;
    public event Action<string> OnError;

    public Lobby HostLobby { get; private set; }
    public Lobby CurrentLobby { get; private set; }

    private float heartbeatTimer;
    private float lobbyUpdateTimer = 10f;

    private string PlayerName => _playerName;
    private string _playerName = "";

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

    private async void Start()
    {
        try
        {
            await UnityServices.InitializeAsync();

            AuthenticationService.Instance.SignedIn += () =>
            {
                Debug.Log($"Signed in: {AuthenticationService.Instance.PlayerId}");
            };

            await AuthenticationService.Instance.SignInAnonymouslyAsync();
            _playerName = "Player " + UnityEngine.Random.Range(1, 9999);

            Debug.Log($"LobbyController signed in as {PlayerName}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Lobby init failed: {ex}");
            OnError?.Invoke(ex.Message);
        }
    }

    private void Update()
    {
        HandleHeartbeat();
        _ = PollForLobbyUpdates();
    }

    private void HandleHeartbeat()
    {
        if (HostLobby == null)
        {
            return;
        }

        heartbeatTimer -= Time.deltaTime;
        float hearbeatTimerMax = 15f;

        if (heartbeatTimer < 0f)
        {
            heartbeatTimer = hearbeatTimerMax;
            _ = Unity.Services.Lobbies.LobbyService.Instance.SendHeartbeatPingAsync(HostLobby.Id);
        }
    }

    private async Task PollForLobbyUpdates()
    {
        if (CurrentLobby == null)
        {
            return;
        }

        lobbyUpdateTimer -= Time.deltaTime;
        if (lobbyUpdateTimer > 0f)
        {
            return;
        }

        lobbyUpdateTimer = 10f; // reset base

        try
        {
            Lobby lobby = await Lobbies.Instance.GetLobbyAsync(CurrentLobby.Id);
            CurrentLobby = lobby;
            MatchSetupData.SyncFromLobby(CurrentLobby);
            OnLobbyUpdated?.Invoke(CurrentLobby);
        }
        catch (LobbyServiceException e) when (e.Reason == LobbyExceptionReason.RateLimited)
        {
            // Exponential backoff
            lobbyUpdateTimer = Mathf.Min(lobbyUpdateTimer * 2, 60f);
            Debug.LogWarning($"Lobby poll rate limited. Next attempt in {lobbyUpdateTimer}s");
        }
        catch (LobbyServiceException e)
        {
            Debug.LogWarning($"Lobby update failed: {e.Message}");
            OnError?.Invoke(e.Message);
            lobbyUpdateTimer = Mathf.Clamp(lobbyUpdateTimer + 5f, 10f, 60f);
        }
    }

    public Lobby GetCurrentLobby() => CurrentLobby;

    public bool IsHost => HostLobby != null && CurrentLobby != null && HostLobby.Id == CurrentLobby.Id;

    public async Task<Lobby> CreateLobbyAsync(
        string lobbyName,
        int maxPlayers,
        string gameMode = "Default",
        string selectedMap = null
    )
    {
        try
        {
            var options = new CreateLobbyOptions
            {
                IsPrivate = false,
                Player = GetPlayer(),
                Data = new Dictionary<string, DataObject>
                {
                    { "GameMode", new DataObject(DataObject.VisibilityOptions.Public, gameMode) },
                    // Host can optionally set the map on create
                    { "SelectedMap", new DataObject(DataObject.VisibilityOptions.Public, selectedMap ?? "") },
                },
            };

            Lobby lobby = await Unity.Services.Lobbies.LobbyService.Instance.CreateLobbyAsync(
                lobbyName,
                maxPlayers,
                options
            );

            HostLobby = lobby;
            CurrentLobby = HostLobby;

            OnLobbyCreated?.Invoke(lobby);
            OnLobbyJoined?.Invoke(lobby);

            // Sync data
            MatchSetupData.SyncFromLobby(CurrentLobby);

            Debug.Log($"Created lobby {lobby.Name} id={lobby.Id} code={lobby.LobbyCode}");

            return lobby;
        }
        catch (LobbyServiceException e)
        {
            Debug.LogException(e);
            OnError?.Invoke(e.Message);

            return null;
        }
    }

    public async Task<List<Lobby>> ListLobbiesAsync()
    {
        try
        {
            QueryLobbiesOptions options = new QueryLobbiesOptions
            {
                Count = 25,
                Filters = new List<QueryFilter>
                {
                    new QueryFilter(QueryFilter.FieldOptions.AvailableSlots, "0", QueryFilter.OpOptions.GT),
                },
                Order = new List<QueryOrder> { new QueryOrder(false, QueryOrder.FieldOptions.Created) },
            };

            QueryResponse resp = await Lobbies.Instance.QueryLobbiesAsync(options);

            var list = new List<RoomInfo>();

            foreach (var lobby in resp.Results)
            {
                int players = lobby.Players?.Count ?? 0;
                var ri = new RoomInfo(lobby.Id, lobby.Name, players, lobby.MaxPlayers, lobby.LobbyCode);
                list.Add(ri);
            }

            OnLobbyListUpdated?.Invoke(list);

            return resp.Results;
        }
        catch (LobbyServiceException e)
        {
            Debug.LogException(e);
            OnError?.Invoke(e.Message);

            return null;
        }
    }

    public async Task<Lobby> JoinLobbyByIdAsync(string id)
    {
        try
        {
            var options = new JoinLobbyByIdOptions { Player = GetPlayer() };
            Lobby lobby = await Lobbies.Instance.JoinLobbyByIdAsync(id, options);
            CurrentLobby = lobby;

            OnLobbyJoined?.Invoke(lobby);

            // Sync data
            MatchSetupData.SyncFromLobby(CurrentLobby);

            if (!IsHost)
            {
                try { }
                catch { }
            }

            Debug.Log($"Joined lobby {lobby.Name}");

            return lobby;
        }
        catch (LobbyServiceException e)
        {
            Debug.LogException(e);
            OnError?.Invoke(e.Message);
            return null;
        }
    }

    public async Task JoinLobbyByCodeAsync(string code)
    {
        try
        {
            var options = new JoinLobbyByCodeOptions { Player = GetPlayer() };

            Lobby lobby = await Lobbies.Instance.JoinLobbyByCodeAsync(code, options);

            CurrentLobby = lobby;

            OnLobbyJoined?.Invoke(lobby);

            // Sync data
            MatchSetupData.SyncFromLobby(CurrentLobby);

            // if you're not the host, start client
            if (!IsHost)
            {
                try { }
                catch { }
            }

            Debug.Log($"Joined lobby {lobby.Name}");
        }
        catch (LobbyServiceException e)
        {
            Debug.LogException(e);
            OnError?.Invoke(e.Message);
        }
    }

    public async Task QuickJoinAsync()
    {
        try
        {
            Lobby lobby = await Lobbies.Instance.QuickJoinLobbyAsync();
            CurrentLobby = lobby;

            OnLobbyJoined?.Invoke(lobby);

            // Sync data
            MatchSetupData.SyncFromLobby(CurrentLobby);

            // if you're not the host, start client
            if (!IsHost)
            {
                try { }
                catch
                { /* tolerant if controller missing in test scenes */
                }
            }
        }
        catch (LobbyServiceException e)
        {
            Debug.LogException(e);
            OnError?.Invoke(e.Message);
        }
    }

    public async Task LeaveLobbyAsync()
    {
        try
        {
            if (CurrentLobby != null)
            {
                await Unity.Services.Lobbies.LobbyService.Instance.RemovePlayerAsync(
                    CurrentLobby.Id,
                    AuthenticationService.Instance.PlayerId
                );

                CurrentLobby = null;
                HostLobby = null;

                // Sync data
                MatchSetupData.SyncFromLobby(CurrentLobby);

                OnLobbyLeft?.Invoke();
            }
        }
        catch (LobbyServiceException e)
        {
            Debug.LogException(e);
            OnError?.Invoke(e.Message);
        }
    }

    public async Task DeleteLobbyAsync()
    {
        try
        {
            if (HostLobby != null)
            {
                await Unity.Services.Lobbies.LobbyService.Instance.DeleteLobbyAsync(HostLobby.Id);
                HostLobby = null;
                CurrentLobby = null;

                // Sync data
                MatchSetupData.SyncFromLobby(CurrentLobby);

                OnLobbyDeleted?.Invoke();
            }
        }
        catch (LobbyServiceException e)
        {
            Debug.LogException(e);
            OnError?.Invoke(e.Message);
        }
    }

    public async Task UpdatePlayerNameAsync(string newName)
    {
        try
        {
            _playerName = newName;
            if (CurrentLobby != null)
            {
                await Unity.Services.Lobbies.LobbyService.Instance.UpdatePlayerAsync(
                    CurrentLobby.Id,
                    AuthenticationService.Instance.PlayerId,
                    new UpdatePlayerOptions
                    {
                        Data = new Dictionary<string, PlayerDataObject>
                        {
                            {
                                "PlayerName",
                                new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, _playerName)
                            },
                        },
                    }
                );
            }
        }
        catch (LobbyServiceException e)
        {
            Debug.LogException(e);
            OnError?.Invoke(e.Message);
        }
    }

    public async Task SetPlayerReadyAsync(bool ready)
    {
        try
        {
            if (CurrentLobby == null)
                return;

            await Unity.Services.Lobbies.LobbyService.Instance.UpdatePlayerAsync(
                CurrentLobby.Id,
                AuthenticationService.Instance.PlayerId,
                new UpdatePlayerOptions
                {
                    Data = new Dictionary<string, PlayerDataObject>
                    {
                        {
                            "IsReady",
                            new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, ready ? "1" : "0")
                        },
                        { "PlayerName", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, _playerName) },
                    },
                }
            );

            // fetch fresh lobby and invoke update event immediately
            // build new snapshot locally to avoid GetLobbyAsync:
            if (CurrentLobby != null)
            {
                var p = CurrentLobby.Players.Find(x => x.Id == AuthenticationService.Instance.PlayerId);
                if (p != null)
                {
                    p.Data["IsReady"] = new PlayerDataObject(
                        PlayerDataObject.VisibilityOptions.Member,
                        ready ? "1" : "0"
                    );
                    p.Data["PlayerName"] = new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, _playerName);
                }

                OnLobbyUpdated?.Invoke(CurrentLobby);
            }
        }
        catch (LobbyServiceException e)
        {
            Debug.LogException(e);
            OnError?.Invoke(e.Message);
        }
    }

    public async Task StartMatchFromHostAsync(string selectedMapScene = "")
    {
        try
        {
            var startTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 3;

            if (HostLobby == null)
            {
                return;
            }

            var update = new UpdateLobbyOptions
            {
                Data = new Dictionary<string, DataObject>
                {
                    { "Started", new DataObject(DataObject.VisibilityOptions.Public, "1") },
                    { "CountdownStartTime", new DataObject(DataObject.VisibilityOptions.Public, startTime.ToString()) },
                },
            };

            //  set the selected map into lobby data so clients can show which map is loading
            if (!string.IsNullOrEmpty(selectedMapScene))
            {
                update.Data["SelectedMap"] = new DataObject(DataObject.VisibilityOptions.Public, selectedMapScene);
            }

            HostLobby = await Lobbies.Instance.UpdateLobbyAsync(HostLobby.Id, update);

            // notify clients by updating joinLobby
            CurrentLobby = HostLobby;
            OnLobbyUpdated?.Invoke(HostLobby);
        }
        catch (LobbyServiceException e)
        {
            Debug.LogException(e);
            OnError?.Invoke(e.Message);
        }
    }

    public static Dictionary<string, (string playerName, bool isReady)> ParsePlayerReadyState(Lobby lobby)
    {
        var dict = new Dictionary<string, (string, bool)>();
        if (lobby == null || lobby.Players == null)
        {
            return dict;
        }

        foreach (var p in lobby.Players)
        {
            string name = p.Data != null && p.Data.TryGetValue("PlayerName", out var pd) ? pd.Value : p.Id;

            bool ready = p.Data != null && p.Data.TryGetValue("IsReady", out var rpd) && rpd.Value == "1";

            dict[p.Id] = (name, ready);
        }
        return dict;
    }

    private Player GetPlayer()
    {
        return new Player
        {
            Data = new Dictionary<string, PlayerDataObject>
            {
                { "PlayerName", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, PlayerName) },
                { "IsReady", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, "0") },
                { "Character", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, "") },
            },
        };
    }

    public async Task SetSelectedCharacterAsync(string characterAssetId)
    {
        try
        {
            if (CurrentLobby == null)
            {
                return;
            }

            await Unity.Services.Lobbies.LobbyService.Instance.UpdatePlayerAsync(
                CurrentLobby.Id,
                AuthenticationService.Instance.PlayerId,
                new UpdatePlayerOptions
                {
                    Data = new Dictionary<string, PlayerDataObject>
                    {
                        {
                            "Character",
                            new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, characterAssetId)
                        },
                        { "PlayerName", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, PlayerName) },
                    },
                }
            );

            var player = CurrentLobby.Players.Find(p => p.Id == AuthenticationService.Instance.PlayerId);

            if (player != null)
            {
                if (player.Data == null)
                {
                    player.Data = new Dictionary<string, PlayerDataObject>();
                }
                player.Data["Character"] = new PlayerDataObject(
                    PlayerDataObject.VisibilityOptions.Member,
                    characterAssetId
                );
            }

            OnLobbyUpdated?.Invoke(CurrentLobby);
        }
        catch (LobbyServiceException e)
        {
            Debug.LogException(e);
            OnError?.Invoke(e.Message);
        }
    }

    public async Task SetSelectedMapAsync(string mapAssetId)
    {
        try
        {
            if (HostLobby == null)
            {
                return;
            }

            HostLobby = await Lobbies.Instance.UpdateLobbyAsync(
                HostLobby.Id,
                new UpdateLobbyOptions
                {
                    Data = new Dictionary<string, DataObject>
                    {
                        { "SelectedMap", new DataObject(DataObject.VisibilityOptions.Public, mapAssetId) },
                    },
                }
            );

            CurrentLobby = HostLobby;
            OnLobbyUpdated?.Invoke(CurrentLobby);
        }
        catch (LobbyServiceException e)
        {
            Debug.LogException(e);
            OnError?.Invoke(e.Message);
        }
    }
}
