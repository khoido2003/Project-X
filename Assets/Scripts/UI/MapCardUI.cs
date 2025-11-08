using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MapCardUI : MonoBehaviour
{
    [SerializeField]
    private Image thumbnail;

    [SerializeField]
    private TextMeshProUGUI mapName;

    [SerializeField]
    private TextMeshProUGUI description;

    [SerializeField]
    private Button selectButton;

    public void Setup(MapDefinitionSO map)
    {
        thumbnail.sprite = map.thumbnail;
        mapName.text = map.displayName;
        description.text = map.description;

        selectButton.onClick.RemoveAllListeners();
        selectButton.onClick.AddListener(() => OnMapSelected(map));
    }

    private void OnMapSelected(MapDefinitionSO map)
    {
        // 1. Offline mode → direct transition
        if (GameFlowService.Instance != null && GameFlowService.Instance.IsOffline)
        {
            UIManager.Instance.OnMapSelected(map);
            return;
        }

        // 2. Online mode → host or client
        if (GameSession.Instance != null)
        {
            GameSession.Instance.CmdSetMap(map.assetId);
        }
        else
        {
            Debug.LogWarning("[MapCardUI] GameSession not yet ready. Deferring map select.");
        }

        UIManager.Instance.OnMapSelected(map);
    }
}
