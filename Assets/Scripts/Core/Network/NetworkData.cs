using System;
using Mirror;

[Serializable]
public class RoomInfo
{
    public string name,
        ip;
    public int players,
        maxPlayers;
}

[System.Serializable]
public struct NetworkCharacterChoice
{
    public string assetId;
    public string displayName;

    public CharacterDefinitionSO GetCharacter() => AssetDatabaseNetwork.GetAsset<CharacterDefinitionSO>(assetId);
}

[System.Serializable]
public struct NetworkMapChoice
{
    public string assetId;
    public string sceneName;

    public MapDefinitionSO GetMap() => AssetDatabaseNetwork.GetAsset<MapDefinitionSO>(assetId);
}
