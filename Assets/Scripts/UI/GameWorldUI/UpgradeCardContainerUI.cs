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

    [Header("Audio")]
    [SerializeField]
    private UISoundConfig uiSoundConfig;

    private UpgradeOption[] _currentOptions;
    private bool _isShowing;
    private float _timeRemaining;
    private bool _hasSelected;
    private float _displayTimer;
    private bool _isSpectatorMode;

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

        _displayTimer += Time.deltaTime;

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

        // Only auto-select after minimum display time (not for spectators)
        // This prevents immediate auto-select due to NetworkVariable sync timing
        const float MIN_DISPLAY_TIME = 2.0f;
        if (
            !_isSpectatorMode
            && timeRemaining <= 0.1f
            && _displayTimer >= MIN_DISPLAY_TIME
            && _currentOptions != null
            && _currentOptions.Length > 0
            && !_hasSelected
        )
        {
            SelectUpgrade(0);
        }

        // Only check phase after grace period
        // NetworkVariable sync can lag behind RPC, causing immediate hide
        const float PHASE_CHECK_GRACE_PERIOD = 1.0f;
        if (
            _displayTimer >= PHASE_CHECK_GRACE_PERIOD
            && NetworkGameStateManager.Instance.CurrentPhase != GamePhase.UpgradePhase
            && _isShowing
        )
        {
            HideUpgradeOptions();
        }
    }

    public void ShowUpgradeOptions(UpgradeOption[] options, bool isSpectatorMode = false)
    {
        if (options == null || options.Length == 0)
        {
            return;
        }

        _currentOptions = options;
        _isShowing = true;
        _hasSelected = false;
        _displayTimer = 0f;
        _isSpectatorMode = isSpectatorMode;

        if (upgradePanel != null)
        {
            upgradePanel.SetActive(true);
        }
        else
        {
            return;
        }

        for (int i = 0; i < upgradeCards.Length; i++)
        {
            if (i < options.Length)
            {
                upgradeCards[i].Setup(options[i], i, this, isSpectatorMode);
                upgradeCards[i].gameObject.SetActive(true);
            }
            else
            {
                upgradeCards[i].gameObject.SetActive(false);
            }
        }

        // Play card appear sound
        if (uiSoundConfig != null && uiSoundConfig.upgradeCardAppear != null && AudioService.Instance != null)
        {
            AudioHelper.PlaySound(uiSoundConfig.upgradeCardAppear, AudioCategory.UI, uiSoundConfig.uiSoundVolume);
        }
    }

    public void HideUpgradeOptions()
    {
        _isShowing = false;
        _currentOptions = null;
        _isSpectatorMode = false;
        upgradePanel.SetActive(false);
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

        // Play selection sound
        if (uiSoundConfig != null && uiSoundConfig.upgradeSelected != null && AudioService.Instance != null)
        {
            AudioHelper.PlaySound(uiSoundConfig.upgradeSelected, AudioCategory.UI, uiSoundConfig.uiSoundVolume);
        }

        if (NetworkUpgradeSystem.Instance != null)
        {
            NetworkUpgradeSystem.Instance.SelectUpgradeServerRpc(
                selectedUpgrade.UpgradeId,
                selectedUpgrade.Value,
                selectedUpgrade.RarityTier
            );
        }
        else
        {
            Debug.LogError("[UpgradeCardUI] NetworkUpgradeSystem.Instance is null!");
        }

        HideUpgradeOptions();
    }
}
