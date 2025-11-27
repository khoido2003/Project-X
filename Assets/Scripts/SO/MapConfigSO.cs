using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Map Definition", fileName = "NewMap")]
public class MapConfigSO : ScriptableObject
{
    [Serializable]
    public class MapData
    {
        public string mapName;
        public SceneName sceneName;
        public Sprite mapPreview;

        [TextArea(2, 4)]
        public string description;
    }

    [Header("Available Maps")]
    public MapData[] availableMaps;

    public MapData GetRandomMap()
    {
        if (availableMaps == null || availableMaps.Length == 0)
        {
            Debug.LogError("No map available in  MapConfig!");
            return null;
        }

        int randomIndex = UnityEngine.Random.Range(0, availableMaps.Length);

        return availableMaps[randomIndex];
    }

    public MapData GetMapBySceneName(SceneName sceneName)
    {
        foreach (var map in availableMaps)
        {
            if (map.sceneName == sceneName)
            {
                return map;
            }
        }

        return null;
    }
}
