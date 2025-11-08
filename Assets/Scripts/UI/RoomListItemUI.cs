using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RoomListItemUI : MonoBehaviour
{
    [SerializeField]
    private TextMeshProUGUI roomInfoText;

    [SerializeField]
    private TextMeshProUGUI playerCountText;

    [SerializeField]
    private Button joinBtn;

    private RoomInfo room;

    public void Setup(RoomInfo r)
    {
        room = r;
        roomInfoText.text = $"{room.name}";
        playerCountText.text = $"({room.players}/{room.maxPlayers})";
        joinBtn.onClick.RemoveAllListeners();
        joinBtn.onClick.AddListener(() =>
        {
            GameFlowService.Instance?.JoinRoom(room);
        });
    }
}
