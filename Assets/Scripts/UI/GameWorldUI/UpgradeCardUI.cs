using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UpgradeCardUI : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField]
    private Image iconImage;

    [SerializeField]
    private TextMeshProUGUI titleText;

    [SerializeField]
    private TextMeshProUGUI descriptionText;

    [SerializeField]
    private TextMeshProUGUI valueText;

    [SerializeField]
    private Button selectBtn;

    [SerializeField]
    private Image cardBackground;

    [SerializeField]
    private Color[] rarityColors = new Color[] { Color.white, Color.green, Color.blue, Color.magenta };

    private UpgradeOption _upgrade;
    private int _cardIndex;
    private UpgradeCardContainerUI _parentUI;

    private void Awake()
    {
        if (selectBtn != null)
        {
            selectBtn.onClick.AddListener(OnSelectedClicked);
        }
    }

    public void Setup(UpgradeOption upgrade, int index, UpgradeCardContainerUI parentUI)
    {
        _upgrade = upgrade;
        _cardIndex = index;
        _parentUI = parentUI;

        if (titleText != null)
        {
            titleText.text = upgrade.Name;
        }

        if (descriptionText != null)
        {
            descriptionText.text = upgrade.Description;
        }

        if (valueText != null)
        {
            string valueStr = upgrade.IsPercentage ? $"+{upgrade.Value}" : $"+{upgrade.Value}";
            valueText.text = valueStr;
        }

        if (iconImage != null)
        {
            iconImage.sprite = GetIconForUpgradeType(upgrade.Type);
        }

        if (cardBackground != null)
        {
            int rarityIndex = Mathf.Clamp(Mathf.FloorToInt(upgrade.Value / 25f), 0, rarityColors.Length - 1);

            cardBackground.color = rarityColors[rarityIndex];
        }
    }

    private void OnSelectedClicked()
    {
        if (_parentUI != null)
        {
            _parentUI.SelectUpgrade(_cardIndex);
        }
    }

    private Sprite GetIconForUpgradeType(UpgradeType type)
    {
        return null;
    }
}
