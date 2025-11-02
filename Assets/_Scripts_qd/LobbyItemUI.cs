using UnityEngine;
using TMPro;

public class LobbyItemUI : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI lobbyNameText;
    public TextMeshProUGUI lobbyLeaderText;
    public TextMeshProUGUI playerCapacityText;
    public TextMeshProUGUI statusText;
    public TextMeshProUGUI presetText;
    public TextMeshProUGUI regionText;

    public void SetupLobbyItem(LobbyData lobbyData, JoinGameManager manager)
    {
        if (lobbyNameText != null)
        {
            lobbyNameText.text = lobbyData.lobbyName;
            lobbyNameText.textWrappingMode = TextWrappingModes.NoWrap;
            lobbyNameText.overflowMode = TextOverflowModes.Truncate;
        }

        if (lobbyLeaderText != null)
        {
            lobbyLeaderText.text = lobbyData.lobbyLeader;
            lobbyLeaderText.textWrappingMode = TextWrappingModes.NoWrap;
            lobbyLeaderText.overflowMode = TextOverflowModes.Truncate;
        }

        if (playerCapacityText != null)
        {
            playerCapacityText.text = lobbyData.playerCapacity;
            playerCapacityText.textWrappingMode = TextWrappingModes.NoWrap;
            playerCapacityText.overflowMode = TextOverflowModes.Truncate;
        }

        if (statusText != null)
        {
            statusText.text = lobbyData.status;
            statusText.textWrappingMode = TextWrappingModes.NoWrap;
            statusText.overflowMode = TextOverflowModes.Truncate;
        }

        if (presetText != null)
        {
            presetText.text = lobbyData.preset;
            presetText.textWrappingMode = TextWrappingModes.NoWrap;
            presetText.overflowMode = TextOverflowModes.Truncate;
        }

        if (regionText != null)
        {
            regionText.text = lobbyData.region;
            regionText.textWrappingMode = TextWrappingModes.NoWrap;
            regionText.overflowMode = TextOverflowModes.Truncate;
        }
    }
}