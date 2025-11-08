using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Mirror network session that holds lobby state. Server authoritative.
/// Publishes events for UI to listen to.
/// </summary>
public class GameSession : NetworkBehaviour
{
    public static GameSession Instance { get; private set; }

    [Header("Room Info")]
    [SyncVar]
    public string roomName = "My Room";

    [SyncVar]
    public int maxPlayers = 6;

    [Header("Game State")]
    [SyncVar]
    public string selectedMapAssetId;

    [SyncVar]
    public float countdown = 5f;

    [SyncVar(hook = nameof(OnReadyCountSync))]
    public int readyCount;

    [SyncVar]
    public bool isCountingDown;

    [NonSerialized]
    public Dictionary<int, NetworkCharacterChoice> playerChoices = new();
    private HashSet<int> readyPlayers = new();

    public class OnPlayerChoiceChangedArgs : EventArgs
    {
        public int playerId;
        public string displayName;
    }

    public event EventHandler<OnPlayerChoiceChangedArgs> OnPlayerChoiceChanged;
    public event Action<int> OnReadyCountChanged;

    // Called when server starts
    public override void OnStartServer()
    {
        Instance = this;
        Debug.Log($"[Server] GameSession started: {roomName}");

        // Register session info into ECS world (entity 0 reserved)
        var sessionComponent = new NetworkSessionComponent
        {
            RoomName = roomName,
            MaxPlayers = maxPlayers,
            SelectedMapAssetId = selectedMapAssetId,
            Countdown = countdown,
            IsCountingDown = isCountingDown,
        };

        try
        {
            if (World.Instance != null && !World.Instance.Components.Has<NetworkSessionComponent>(new EntityId(0)))
            {
                World.Instance.Components.Add(new EntityId(0), sessionComponent);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to register NetworkSessionComponent: {ex.Message}");
        }
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        Instance = this;
        if (!isServer)
            Debug.Log("[Client] Connected to session.");
    }

    [Command(requiresAuthority = false)]
    public void CmdChooseCharacter(string characterAssetID)
    {
        int playerId = connectionToClient.connectionId;
        var character = AssetDatabaseNetwork.GetAsset<CharacterDefinitionSO>(characterAssetID);
        if (character == null)
        {
            Debug.LogWarning($"CmdChooseCharacter: Character asset not found: {characterAssetID}");
            return;
        }

        playerChoices[playerId] = new NetworkCharacterChoice
        {
            assetId = characterAssetID,
            displayName = character.characterName,
        };

        // Notify all clients
        RpcPlayerChoseCharacter(playerId, characterAssetID, character.characterName);

        if (isServer && isClient)
        {
            OnPlayerChoiceChanged?.Invoke(
                this,
                new OnPlayerChoiceChangedArgs { playerId = playerId, displayName = character.characterName }
            );
        }

        Debug.Log($"[Server] Player {playerId} chose {character.characterName}");
    }

    [Command(requiresAuthority = false)]
    public void CmdToggleReady()
    {
        int playerId = connectionToClient.connectionId;
        if (readyPlayers.Contains(playerId))
            readyPlayers.Remove(playerId);
        else
            readyPlayers.Add(playerId);

        readyCount = readyPlayers.Count;

        if (isServer && isClient)
            OnReadyCountSync(readyCount, readyCount);

        // update ECS session component if present
        try
        {
            if (World.Instance != null && World.Instance.Components.Has<NetworkSessionComponent>(new EntityId(0)))
            {
                var comp = World.Instance.Components.Get<NetworkSessionComponent>(new EntityId(0));
                comp.ReadyPlayers = new HashSet<int>(readyPlayers);
                comp.Countdown = countdown;
                comp.IsCountingDown = isCountingDown;
            }
        }
        catch { }
    }

    [Command(requiresAuthority = false)]
    public void CmdSetMap(string mapAssetId)
    {
        selectedMapAssetId = mapAssetId;
        RpcSyncMap(mapAssetId);

        try
        {
            if (World.Instance != null && World.Instance.Components.Has<NetworkSessionComponent>(new EntityId(0)))
            {
                var comp = World.Instance.Components.Get<NetworkSessionComponent>(new EntityId(0));
                comp.SelectedMapAssetId = mapAssetId;
            }
        }
        catch { }
    }

    [ClientRpc]
    private void RpcSyncMap(string mapAssetId)
    {
        selectedMapAssetId = mapAssetId;
    }

    [ClientRpc]
    private void RpcPlayerChoseCharacter(int playerId, string characterId, string displayName)
    {
        // On clients, raise the event in a client-friendly way
        OnPlayerChoiceChanged?.Invoke(
            this,
            new OnPlayerChoiceChangedArgs { playerId = playerId, displayName = displayName }
        );
    }

    [Server]
    public void StartMatch()
    {
        // require at least 2 players and all ready (server authoritative)
        if (readyCount == playerChoices.Count && playerChoices.Count >= 2 && !isCountingDown)
        {
            isCountingDown = true;
            countdown = 5f;
            RpcCountdown(countdown);
            InvokeRepeating(nameof(CountdownTick), 1f, 1f);
        }
        else
        {
            Debug.Log("[Server] StartMatch conditions not met (need min 2 players and all ready).");
        }
    }

    [Server]
    void CountdownTick()
    {
        countdown--;
        RpcCountdown(countdown);

        if (countdown <= 0)
        {
            CancelInvoke(nameof(CountdownTick));
            RpcLoadGame();
        }
    }

    [ClientRpc]
    void RpcCountdown(float time)
    {
        // Clients show countdown via UIManager
        if (UIManager.Instance != null)
            UIManager.Instance.ShowCountdown(time);
    }

    [ClientRpc]
    void RpcLoadGame()
    {
        // Ask GameFlowService to load the scene locally (clients)
        if (GameFlowService.Instance != null)
        {
            GameFlowService.Instance.LoadMapByAssetId(selectedMapAssetId);
        }
        else
        {
            var map = AssetDatabaseNetwork.GetAsset<MapDefinitionSO>(selectedMapAssetId);
            if (map != null)
                SceneManager.LoadScene(map.sceneName);
        }
    }

    [Server]
    public void StartHost(string roomName)
    {
        this.roomName = roomName;
        NetworkServer.Spawn(gameObject);
    }

    private void OnReadyCountSync(int oldCount, int newCount)
    {
        try
        {
            OnReadyCountChanged?.Invoke(newCount);
        }
        catch (Exception) { }
    }
}
