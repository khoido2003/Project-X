using UnityEngine;

public static class OfflineGameData
{
    public static MapDefinitionSO SelectedMap;
    public static CharacterDefinitionSO SelectedCharacter;
}

public class GameFlowService : MonoBehaviour
{
    public static GameFlowService Instance { get; private set; }

    public bool IsOffline { get; private set; }
    public bool IsHost { get; private set; }

    private GameNetworkManager _networkManager;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        _networkManager = FindFirstObjectByType<GameNetworkManager>();
    }

    // -------------------------
    // OFFLINE
    // -------------------------
    public void StartOffline(MapDefinitionSO map, CharacterDefinitionSO character)
    {
        IsOffline = true;
        IsHost = true;
        OfflineGameData.SelectedMap = map;
        OfflineGameData.SelectedCharacter = character;

        LoadingSceneController.LoadScene(map.sceneName);
    }

    // -------------------------
    // HOST
    // -------------------------
    public void StartHost(string roomName)
    {
        IsOffline = false;
        IsHost = true;

        if (_networkManager == null)
        {
            _networkManager = FindFirstObjectByType<GameNetworkManager>();

            Debug.Log(_networkManager == null);
        }

        if (_networkManager != null)
        {
            _networkManager.StartHostRoom(roomName);
        }
        else
        {
            Debug.LogError("GameNetworkManager not found in the scene.");
        }
    }

    // -------------------------
    // JOIN
    // -------------------------
    public void JoinRoom(RoomInfo room)
    {
        IsOffline = false;
        IsHost = false;

        if (_networkManager == null)
            _networkManager = FindFirstObjectByType<GameNetworkManager>();

        if (_networkManager != null)
        {
            _networkManager.JoinRoom(room.ip);
        }
        else
        {
            Debug.LogError("GameNetworkManager not found in the scene.");
        }
    }

    // Called by GameSession when server tells clients to load the map
    public void LoadMapByAssetId(string mapAssetId)
    {
        var map = AssetDatabaseNetwork.GetAsset<MapDefinitionSO>(mapAssetId);
        if (map != null)
        {
            LoadingSceneController.LoadScene(map.sceneName);
        }
        else
        {
            Debug.LogError($"GameFlowService.LoadMapByAssetId: map '{mapAssetId}' not found");
        }
    }

    public void SetOfflineMode(bool value)
    {
        IsOffline = value;
    }

    public void SetHostMode(bool value)
    {
        IsHost = value;
    }
}
