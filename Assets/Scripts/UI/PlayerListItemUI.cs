using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerListItemUI : MonoBehaviour
{
    [SerializeField]
    private TextMeshProUGUI playerInfoText;

    [SerializeField]
    private TextMeshProUGUI readyIndicator;

    [SerializeField]
    private TextMeshProUGUI heroChoose;

    public void Setup(int playerId, string characterName, bool isReady)
    {
        playerInfoText.text = $"Player {playerId}";
        heroChoose.text = $"{characterName}";
        readyIndicator.gameObject.SetActive(true);
        readyIndicator.text = isReady ? "READY" : "NOT READY";
        readyIndicator.color = isReady ? Color.green : Color.red;
    }
}
