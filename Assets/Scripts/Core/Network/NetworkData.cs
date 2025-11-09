using System;
using Mirror;

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
