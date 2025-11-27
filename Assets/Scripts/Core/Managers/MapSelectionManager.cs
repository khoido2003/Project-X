using System;
using Unity.Netcode;
using UnityEngine;

public class MapSelectionManager : SingletonNetwork<MapSelectionManager>
{
    [SerializeField]
    private MapConfigSO mapConfig;

    private NetworkVariable<int> m_selectedMapIndex = new(
        -1,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public SceneName SelectedMapScene { get; private set; }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsServer)
        {
            SelectRandomMap();
        }

        m_selectedMapIndex.OnValueChanged += OnMapIndexChanged;

        if (m_selectedMapIndex.Value >= 0)
        {
            ApplyMapSelection(m_selectedMapIndex.Value);
        }
    }

    private void OnDisable()
    {
        m_selectedMapIndex.OnValueChanged -= OnMapIndexChanged;
    }

    /// <summary>
    /// Server-only: Selects a random map from the configuration
    /// </summary>
    public void SelectRandomMap()
    {
        if (!IsServer)
        {
            Debug.LogWarning("Only server can select map!");
            return;
        }

        if (mapConfig == null || mapConfig.availableMaps.Length == 0)
        {
            Debug.LogError("MapConfig not set or empty!");
            m_selectedMapIndex.Value = -1;
            SelectedMapScene = SceneName.Map_1;

            return;
        }

        var randomMap = mapConfig.GetRandomMap();
        int mapIndex = Array.IndexOf(mapConfig.availableMaps, randomMap);

        m_selectedMapIndex.Value = mapIndex;
        SelectedMapScene = randomMap.sceneName;
    }

    /// <summary>
    /// Server-only: Manually select a specific map by index
    /// </summary>
    public void SelectMapByIndex(int index)
    {
        if (!IsServer)
        {
            Debug.LogWarning("Only server can select map!");
            return;
        }

        if (index < 0 || index >= mapConfig.availableMaps.Length)
        {
            Debug.LogError($"Invalid map index: {index}");
            return;
        }

        m_selectedMapIndex.Value = index;
        SelectedMapScene = mapConfig.availableMaps[index].sceneName;
    }

    private void OnMapIndexChanged(int previousValue, int newValue) { }

    private void ApplyMapSelection(int mapIndex)
    {
        if (mapConfig == null || mapIndex >= mapConfig.availableMaps.Length)
        {
            Debug.LogError($"Invalid map index: {mapIndex}");
            SelectedMapScene = SceneName.Map_1;
            return;
        }

        MapConfigSO.MapData selectedMap = mapConfig.availableMaps[mapIndex];
        SelectedMapScene = selectedMap.sceneName;

        Debug.Log($"Client {NetworkManager.Singleton.LocalClientId}: Map selected {selectedMap.mapName}");
    }

    [ClientRpc]
    private void NotifyMapSelectionClientRpc(string mapName)
    {
        Debug.Log($"Playing on map: {mapName}");
    }

    public string GetCurrentMapName()
    {
        if (m_selectedMapIndex.Value < 0 || m_selectedMapIndex.Value >= mapConfig.availableMaps.Length)
        {
            return "Unknow";
        }

        return mapConfig.availableMaps[m_selectedMapIndex.Value].mapName;
    }
}
