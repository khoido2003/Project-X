// MapCardUI.cs
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MapCardUI : MonoBehaviour
{
    [SerializeField] private Image thumbnail;
    [SerializeField] private TextMeshProUGUI mapName;
    [SerializeField] private TextMeshProUGUI description;
    [SerializeField] private Button selectButton;

    private MapDefinitionSO map;

    public void Setup(MapDefinitionSO m)
    {
        map = m;
        thumbnail.sprite = map.thumbnail;
        mapName.text = map.displayName;
        description.text = map.description;

        selectButton.onClick.RemoveAllListeners();
        selectButton.onClick.AddListener(OnSelect);
    }

    private void OnSelect()
    {
        UIManager.Instance?.OnMapSelected(map);
    }
}
