using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;

public class LobbyController : MonoBehaviour
{
    public static LobbyController Instance { get; private set; }

    public event Action<Lobby> OnLobbyCreated;
    public event Action<Lobby> OnLobbyJoined;
    public event Action<List<RoomInfo>> OnLobbyListUpdated;
    public event Action<Lobby> OnLobbyUpdated;
    public event Action OnLobbyLeft;
    public event Action<string> OnError;
    public event Action OnLobbyDeleted;

    private Lobby hostLobby;
    private Lobby joinedLobby;

    private float heartbeatTimer;

    private float lobbyUpdateTimer = 10f;
    private float lobbyUpdateInterval = 10f;
    private int lobbyErrorBackoffCount = 0;
    private const int MAX_BACKOFF_EXP = 6;

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
        if (hostLobby != null)
        {
            heartbeatTimer -= Time.deltaTime;
            if (heartbeatTimer < 0f)
            {
                heartbeatTimer = 15f;
                _ = LobbyService.Instance.SendHeartbeatPingAsync(hostLobby.Id);
            }
        }
    }

    private async Task PollForLobbyUpdates()
    {
        if (joinedLobby == null)
            return;

        lobbyUpdateTimer -= Time.deltaTime;
        if (lobbyUpdateTimer > 0f)
            return;

        try
        {
            int exponent = Mathf.Min(lobbyErrorBackoffCount, MAX_BACKOFF_EXP);
            int multiplier = 1;
            for (int i = 0; i < exponent; i++)
            {
                multiplier *= 2;
            }
            lobbyUpdateTimer = lobbyUpdateInterval * multiplier;

            Lobby lobby = await LobbyService.Instance.GetLobbyAsync(joinedLobby.Id);

            joinedLobby = lobby;
            lobbyErrorBackoffCount = 0;
            MatchSetupData.SyncFromLobby(joinedLobby);

            OnLobbyUpdated?.Invoke(joinedLobby);
        }
        catch (LobbyServiceException e)
        {
            Debug.LogWarning($"Lobby update failed: {e}. Backoff attempt={lobbyErrorBackoffCount}");

            lobbyErrorBackoffCount++;
            OnError?.Invoke(e.Message);

            // if rate limited (429) we can set a larger timer
            if (e.Message != null && e.Message.Contains("Too Many Requests", StringComparison.OrdinalIgnoreCase))
            {
                lobbyUpdateTimer += 10f;
            }

            // clamp timer, don't retry too often
            lobbyUpdateTimer = Mathf.Clamp(lobbyUpdateTimer, 5f, 300f);
        }
    }

    public Lobby GetCurrentLobby() => joinedLobby;

    public bool IsHost => hostLobby != null && joinedLobby != null && hostLobby.Id == joinedLobby.Id;

    public async Task CreateLobbyAsync(
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

            Lobby lobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, maxPlayers, options);

            hostLobby = lobby;
            joinedLobby = hostLobby;

            OnLobbyCreated?.Invoke(lobby);
            OnLobbyJoined?.Invoke(lobby);

            // Sync data
            MatchSetupData.SyncFromLobby(joinedLobby);

            Debug.Log($"Created lobby {lobby.Name} id={lobby.Id} code={lobby.LobbyCode}");
        }
        catch (LobbyServiceException e)
        {
            Debug.LogException(e);
            OnError?.Invoke(e.Message);
        }
    }

    public async Task ListLobbiesAsync()
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
        }
        catch (LobbyServiceException e)
        {
            Debug.LogException(e);
            OnError?.Invoke(e.Message);
        }
    }

    public async Task JoinLobbyByIdAsync(string id)
    {
        try
        {
            var options = new JoinLobbyByIdOptions { Player = GetPlayer() };
            Lobby lobby = await Lobbies.Instance.JoinLobbyByIdAsync(id, options);
            joinedLobby = lobby;

            OnLobbyJoined?.Invoke(lobby);

            // Sync data
            MatchSetupData.SyncFromLobby(joinedLobby);

            // if you're not the host, start client
            if (!IsHost)
            {
                try
                {
                    NetworkSessionController.Instance?.StartClient();
                }
                catch
                { /* tolerant if controller missing in test scenes */
                }
            }

            Debug.Log($"Joined lobby {lobby.Name}");
        }
        catch (LobbyServiceException e)
        {
            Debug.LogException(e);
            OnError?.Invoke(e.Message);
        }
    }

    public async Task JoinLobbyByCodeAsync(string code)
    {
        try
        {
            var options = new JoinLobbyByCodeOptions { Player = GetPlayer() };

            Lobby lobby = await Lobbies.Instance.JoinLobbyByCodeAsync(code, options);

            joinedLobby = lobby;

            OnLobbyJoined?.Invoke(lobby);

            // Sync data
            MatchSetupData.SyncFromLobby(joinedLobby);

            // if you're not the host, start client
            if (!IsHost)
            {
                try
                {
                    NetworkSessionController.Instance?.StartClient();
                }
                catch
                { /* tolerant if controller missing in test scenes */
                }
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
            joinedLobby = lobby;

            OnLobbyJoined?.Invoke(lobby);

            // Sync data
            MatchSetupData.SyncFromLobby(joinedLobby);

            // if you're not the host, start client
            if (!IsHost)
            {
                try
                {
                    NetworkSessionController.Instance?.StartClient();
                }
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
            if (joinedLobby != null)
            {
                await LobbyService.Instance.RemovePlayerAsync(joinedLobby.Id, AuthenticationService.Instance.PlayerId);

                joinedLobby = null;
                hostLobby = null;

                // Sync data
                MatchSetupData.SyncFromLobby(joinedLobby);

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
            if (hostLobby != null)
            {
                await LobbyService.Instance.DeleteLobbyAsync(hostLobby.Id);
                hostLobby = null;
                joinedLobby = null;

                // Sync data
                MatchSetupData.SyncFromLobby(joinedLobby);

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
            if (joinedLobby != null)
            {
                await LobbyService.Instance.UpdatePlayerAsync(
                    joinedLobby.Id,
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
            if (joinedLobby == null)
                return;

            await LobbyService.Instance.UpdatePlayerAsync(
                joinedLobby.Id,
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
            if (joinedLobby != null)
            {
                var p = joinedLobby.Players.Find(x => x.Id == AuthenticationService.Instance.PlayerId);
                if (p != null)
                {
                    p.Data["IsReady"] = new PlayerDataObject(
                        PlayerDataObject.VisibilityOptions.Member,
                        ready ? "1" : "0"
                    );
                    p.Data["PlayerName"] = new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, _playerName);
                }

                OnLobbyUpdated?.Invoke(joinedLobby);
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

            if (hostLobby == null)
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

            hostLobby = await Lobbies.Instance.UpdateLobbyAsync(hostLobby.Id, update);

            // notify clients by updating joinLobby
            joinedLobby = hostLobby;
            OnLobbyUpdated?.Invoke(hostLobby);
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
            if (joinedLobby == null)
            {
                return;
            }

            await LobbyService.Instance.UpdatePlayerAsync(
                joinedLobby.Id,
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

            joinedLobby = await LobbyService.Instance.GetLobbyAsync(joinedLobby.Id);

            OnLobbyUpdated?.Invoke(joinedLobby);
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
            if (hostLobby == null)
            {
                return;
            }

            hostLobby = await Lobbies.Instance.UpdateLobbyAsync(
                hostLobby.Id,
                new UpdateLobbyOptions
                {
                    Data = new Dictionary<string, DataObject>
                    {
                        { "SelectedMap", new DataObject(DataObject.VisibilityOptions.Public, mapAssetId) },
                    },
                }
            );

            joinedLobby = hostLobby;
            OnLobbyUpdated?.Invoke(joinedLobby);
        }
        catch (LobbyServiceException e)
        {
            Debug.LogException(e);
            OnError?.Invoke(e.Message);
        }
    }
}
