using UnityEngine;

[CreateAssetMenu(menuName = "Game/Map Definition", fileName = "NewMap")]
public class MapDefinitionSO : NetworkSO
{
    [Header("Scene")]
    public string sceneName;
    public string displayName;
    public Sprite thumbnail;

    [Header("Rules")]
    public int maxPlayers = 4;
    public bool randomizePlayerSpawns = true;

    [Header("Safety / metadata")]
    public string description;
}
