using System.Collections.Generic;
using UnityEngine;

public class NetworkSessionComponent : MonoBehaviour
{
    public string RoomName;
    public int MaxPlayers;
    public string SelectedMapAssetId;

    public Dictionary<int, NetworkCharacterChoice> PlayerChoices = new();
    public HashSet<int> ReadyPlayers = new();

    public bool IsCountingDown;
    public float Countdown;
}
