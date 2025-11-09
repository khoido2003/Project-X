// LobbyController.cs
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

    // Events
    public event Action<Lobby> OnLobbyCreated;
    public event Action<Lobby> OnLobbyJoined;
    public event Action<List<RoomInfo>> OnLobbyListUpdated;
    public event Action<Lobby> OnLobbyUpdated;
    public event Action OnLobbyLeft;
    public event Action<string> OnError;
    public event Action OnLobbyDeleted;

    private Lobby hostLobby;
    private Lobby joinLobby;

    private float heartbeatTimer;
    private float lobbyUpdateTimer;

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
            _playerName = "Player " + UnityEngine.Random.Range(10, 99);

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
        if (joinLobby != null)
        {
            lobbyUpdateTimer -= Time.deltaTime;
            if (lobbyUpdateTimer < 0f)
            {
                lobbyUpdateTimer = 5f;
                try
                {
                    Lobby lobby = await LobbyService.Instance.GetLobbyAsync(joinLobby.Id);
                    joinLobby = lobby;
                    OnLobbyUpdated?.Invoke(joinLobby);
                }
                catch (LobbyServiceException e)
                {
                    Debug.LogWarning($"Lobby update failed: {e}");
                    OnError?.Invoke(e.Message);
                }
            }
        }
    }

    public async Task CreateLobbyAsync(string lobbyName, int maxPlayers, string gameMode = "Default")
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
                },
            };

            Lobby lobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, maxPlayers, options);

            hostLobby = lobby;
            joinLobby = hostLobby;

            OnLobbyCreated?.Invoke(lobby);
            OnLobbyJoined?.Invoke(lobby);

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

    public async Task JoinLobbyByCodeAsync(string code)
    {
        try
        {
            var options = new JoinLobbyByCodeOptions { Player = GetPlayer() };

            Lobby lobby = await Lobbies.Instance.JoinLobbyByCodeAsync(code, options);

            joinLobby = lobby;

            OnLobbyJoined?.Invoke(lobby);
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
            joinLobby = lobby;

            OnLobbyJoined?.Invoke(lobby);
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
            if (joinLobby != null)
            {
                await LobbyService.Instance.RemovePlayerAsync(joinLobby.Id, AuthenticationService.Instance.PlayerId);

                joinLobby = null;
                hostLobby = null;

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
                joinLobby = null;
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
            if (joinLobby != null)
            {
                await LobbyService.Instance.UpdatePlayerAsync(
                    joinLobby.Id,
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
            if (joinLobby == null)
                return;

            await LobbyService.Instance.UpdatePlayerAsync(
                joinLobby.Id,
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
            joinLobby = await LobbyService.Instance.GetLobbyAsync(joinLobby.Id);
            OnLobbyUpdated?.Invoke(joinLobby);
        }
        catch (LobbyServiceException e)
        {
            Debug.LogException(e);
            OnError?.Invoke(e.Message);
        }
    }

    /// <summary>
    /// When host wants to start, mark lobby data and return. Client/Server must then coordinate scene loads.
    /// </summary>
    public async Task StartMatchFromHostAsync()
    {
        try
        {
            if (hostLobby == null)
                return;

            // Mark the lobby as started
            hostLobby = await Lobbies.Instance.UpdateLobbyAsync(
                hostLobby.Id,
                new UpdateLobbyOptions
                {
                    Data = new Dictionary<string, DataObject>
                    {
                        { "Started", new DataObject(DataObject.VisibilityOptions.Public, "1") },
                    },
                }
            );

            // notify clients by updating joinLobby (poll will detect)
            joinLobby = hostLobby;
            OnLobbyUpdated?.Invoke(hostLobby);
        }
        catch (LobbyServiceException e)
        {
            Debug.LogException(e);
            OnError?.Invoke(e.Message);
        }
    }

    /// <summary>
    /// Utility: read player ready state from a Lobby object. Returns dictionary playerId -> (playerName, isReady)
    /// </summary>
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
            },
        };
    }
}
