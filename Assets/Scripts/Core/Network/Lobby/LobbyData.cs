using System.Collections.Generic;
using Unity.Services.Lobbies.Models;

public class RoomInfo
{
    public string Id;
    public string Name;
    public int Players;
    public int MaxPlayers;
    public string LobbyCode;

    public RoomInfo() { }

    public RoomInfo(string id, string name, int players, int maxPlayers, string lobbyCode)
    {
        Id = id;
        Name = name;
        Players = players;
        MaxPlayers = maxPlayers;
        LobbyCode = lobbyCode;
    }
}

public static class MatchSetupData
{
    public static string SelectedMapId;
    public static Dictionary<string, string> PlayerCharacterSelections = new();

    public static void SyncFromLobby(Lobby lobby)
    {
        PlayerCharacterSelections.Clear();
        if (lobby == null)
        {
            return;
        }

        if (lobby.Data != null && lobby.Data.TryGetValue("SelectedMap", out var map))
        {
            SelectedMapId = map.Value;
        }

        foreach (var p in lobby.Players)
        {
            if (p.Data.TryGetValue("Character", out var charData))
            {
                PlayerCharacterSelections[p.Id] = charData.Value;
            }
        }
    }
}
