using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UpgradeCardContainerUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField]
    private GameObject upgradePanel;

    [SerializeField]
    private UpgradeCardUI[] upgradeCards = new UpgradeCardUI[3];

    [Header("Timer")]
    [SerializeField]
    private TextMeshProUGUI timeText;

    [SerializeField]
    private Slider timerFillBar;

    private UpgradeOption[] _currentOptions;
    private bool _isShowing;
    private float _timeRemaining;
    private bool _hasSelected;

    private void Awake()
    {
        upgradePanel.SetActive(false);
    }

    private void Update()
    {
        if (!_isShowing || _hasSelected)
        {
            return;
        }

        if (NetworkGameStateManager.Instance == null)
        {
            return;
        }

        float timeRemaining = NetworkGameStateManager.Instance.PhaseTimeRemaining;

        if (timeText != null)
        {
            timeText.text = $"Time: {Mathf.CeilToInt(timeRemaining)}s";
        }

        if (timerFillBar != null)
        {
            float maxTime = GameConstants.UPGRADE_PHASE_DURATION;
            timerFillBar.value = timeRemaining / maxTime;
        }

        // Auto-select only when timer actually runs out
        if (timeRemaining <= 0.1f && _currentOptions != null && _currentOptions.Length > 0 && !_hasSelected)
        {
            Debug.Log("[UpgradeCardUI] Time expired, auto-selecting first upgrade");
            SelectUpgrade(0);
        }

        if (NetworkGameStateManager.Instance.CurrentPhase != GamePhase.UpgradePhase && _isShowing)
        {
            Debug.Log("[UpgradeCardUI] Phase changed, hiding upgrade panel");
            HideUpgradeOptions();
        }
    }

    public void ShowUpgradeOptions(UpgradeOption[] options)
    {
        if (options == null || options.Length == 0)
        {
            Debug.LogError("[UpgradeCardUI] Received null or empty options!");
            return;
        }

        _currentOptions = options;
        _isShowing = true;
        _hasSelected = false;

        if (upgradePanel != null)
        {
            upgradePanel.SetActive(true);
        }
        else
        {
            Debug.LogError("[UpgradeCardUI] upgradePanel is null!");
            return;
        }

        for (int i = 0; i < upgradeCards.Length; i++)
        {
            if (i < options.Length)
            {
                upgradeCards[i].Setup(options[i], i, this);
                upgradeCards[i].gameObject.SetActive(true);
            }
            else
            {
                upgradeCards[i].gameObject.SetActive(false);
            }
        }

        Debug.Log($"[UpgradeCardUI] Showing {options.Length} upgrade options");
    }

    public void HideUpgradeOptions()
    {
        _isShowing = false;
        _currentOptions = null;
        upgradePanel.SetActive(false);

        Debug.Log("[UpgradeCardUI] Hidden upgrade options");
    }

    public void SelectUpgrade(int cardIndex)
    {
        if (_hasSelected)
        {
            Debug.LogWarning("[UpgradeCardUI] Already selected an upgrade, ignoring");
            return;
        }

        if (_currentOptions == null || cardIndex >= _currentOptions.Length)
        {
            Debug.LogError($"[UpdateCardUI] Invalid card index: {cardIndex}");
            return;
        }

        _hasSelected = true;

        var selectedUpgrade = _currentOptions[cardIndex];
        Debug.Log($"[UpgradeCardUI] Selected upgrade: {selectedUpgrade.Name}");

        if (NetworkUpgradeSystem.Instance != null)
        {
            NetworkUpgradeSystem.Instance.SelectUpgradeServerRpc(selectedUpgrade.UpgradeId);
        }
        else
        {
            Debug.LogError("[UpgradeCardUI] NetworkUpgradeSystem.Instance is null!");
        }

        HideUpgradeOptions();
    }
}
