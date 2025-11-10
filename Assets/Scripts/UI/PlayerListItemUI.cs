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

    public void Setup(string playerName, string characterName, bool isReady)
    {
        playerInfoText.text = $"{playerName}";
        heroChoose.text = $"{characterName}";
        readyIndicator.gameObject.SetActive(true);
        readyIndicator.text = isReady ? "READY" : "NOT READY";
        readyIndicator.color = isReady ? Color.green : Color.red;
    }
}
