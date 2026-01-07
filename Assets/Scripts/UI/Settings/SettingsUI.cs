using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// UI controller for the settings panel.
/// Attach this to a Settings Panel prefab with the required UI elements.
/// </summary>
public class SettingsUI : MonoBehaviour
{
    [Header("Panel References")]
    [SerializeField]
    private GameObject settingsPanel;

    [SerializeField]
    private Button openSettingsButton;

    [SerializeField]
    private Button closeButton;

    [Header("Audio Settings")]
    [SerializeField]
    private Slider masterVolumeSlider;

    [SerializeField]
    private TextMeshProUGUI masterVolumeText;

    [SerializeField]
    private Slider musicVolumeSlider;

    [SerializeField]
    private TextMeshProUGUI musicVolumeText;

    [SerializeField]
    private Slider sfxVolumeSlider;

    [SerializeField]
    private TextMeshProUGUI sfxVolumeText;

    [Header("Display Settings")]
    [SerializeField]
    private Toggle fullscreenToggle;

    [SerializeField]
    private Toggle vsyncToggle;

    [SerializeField]
    private TMP_Dropdown qualityDropdown;

    [SerializeField]
    private TMP_Dropdown aspectRatioDropdown;

    [Header("Buttons")]
    [SerializeField]
    private Button applyButton;

    [SerializeField]
    private Button resetDefaultsButton;

    [Header("Audio")]
    [SerializeField]
    private UISoundConfig uiSoundConfig;

    private bool _isInitialized;
    private float _lastSliderChangeTime;
    private float _lastSliderValue;
    private bool _sliderWasAdjusted;
    private const float SLIDER_SOUND_DELAY = 0.3f; // Play sound after user stops adjusting

    private void Start()
    {
        InitializeUI();
    }

    private void Update()
    {
        // Play preview sound after user stops adjusting SFX slider
        if (_sliderWasAdjusted && Time.time - _lastSliderChangeTime > SLIDER_SOUND_DELAY)
        {
            PlayClickSound();
            _sliderWasAdjusted = false;
        }
    }

    private void InitializeUI()
    {
        if (_isInitialized) return;

        // Setup button listeners
        if (openSettingsButton != null)
        {
            openSettingsButton.onClick.AddListener(OpenSettings);
        }

        if (closeButton != null)
        {
            closeButton.onClick.AddListener(CloseSettings);
        }

        if (applyButton != null)
        {
            applyButton.onClick.AddListener(ApplySettings);
        }

        if (resetDefaultsButton != null)
        {
            resetDefaultsButton.onClick.AddListener(ResetToDefaults);
        }

        // Setup slider listeners
        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
        }

        if (musicVolumeSlider != null)
        {
            musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
        }

        if (sfxVolumeSlider != null)
        {
            sfxVolumeSlider.onValueChanged.AddListener(OnSFXVolumeChanged);
        }

        // Setup toggle listeners
        if (fullscreenToggle != null)
        {
            fullscreenToggle.onValueChanged.AddListener(OnFullscreenChanged);
        }

        if (vsyncToggle != null)
        {
            vsyncToggle.onValueChanged.AddListener(OnVSyncChanged);
        }

        // Setup quality dropdown with our custom presets
        if (qualityDropdown != null)
        {
            qualityDropdown.ClearOptions();
            
            // Use custom quality names (Low, Medium, High) from SettingsManager
            string[] qualityNames = SettingsManager.Instance != null 
                ? SettingsManager.Instance.GetQualityNames() 
                : new string[] { "Low", "Medium", "High" };
            
            qualityDropdown.AddOptions(new System.Collections.Generic.List<string>(qualityNames));
            qualityDropdown.onValueChanged.AddListener(OnQualityChanged);
        }

        // Setup aspect ratio dropdown
        if (aspectRatioDropdown != null)
        {
            aspectRatioDropdown.ClearOptions();
            
            string[] aspectRatioNames = SettingsManager.Instance != null 
                ? SettingsManager.Instance.GetAspectRatioNames() 
                : new string[] { "16:9", "16:10" };
            
            aspectRatioDropdown.AddOptions(new System.Collections.Generic.List<string>(aspectRatioNames));
            aspectRatioDropdown.onValueChanged.AddListener(OnAspectRatioChanged);
        }

        // Hide panel initially
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }

        _isInitialized = true;
    }

    /// <summary>
    /// Opens the settings panel and loads current settings.
    /// </summary>
    public void OpenSettings()
    {
        PlayClickSound();

        if (!_isInitialized)
        {
            InitializeUI();
        }

        LoadCurrentSettings();

        if (settingsPanel != null)
        {
            settingsPanel.SetActive(true);
        }
    }

    /// <summary>
    /// Closes the settings panel.
    /// </summary>
    public void CloseSettings()
    {
        PlayClickSound();

        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }
    }

    /// <summary>
    /// Loads current settings values into UI elements.
    /// </summary>
    private void LoadCurrentSettings()
    {
        if (SettingsManager.Instance == null)
        {
            Debug.LogWarning("[SettingsUI] SettingsManager.Instance is null");
            return;
        }

        // Load audio settings
        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.value = SettingsManager.Instance.GetMasterVolume();
            UpdateVolumeText(masterVolumeText, masterVolumeSlider.value);
        }

        if (musicVolumeSlider != null)
        {
            musicVolumeSlider.value = SettingsManager.Instance.GetMusicVolume();
            UpdateVolumeText(musicVolumeText, musicVolumeSlider.value);
        }

        if (sfxVolumeSlider != null)
        {
            sfxVolumeSlider.value = SettingsManager.Instance.GetSFXVolume();
            UpdateVolumeText(sfxVolumeText, sfxVolumeSlider.value);
        }

        // Load display settings
        if (fullscreenToggle != null)
        {
            fullscreenToggle.isOn = SettingsManager.Instance.GetFullscreen();
        }

        if (vsyncToggle != null)
        {
            vsyncToggle.isOn = SettingsManager.Instance.GetVSync();
        }

        if (qualityDropdown != null)
        {
            qualityDropdown.value = SettingsManager.Instance.GetQuality();
        }

        if (aspectRatioDropdown != null)
        {
            aspectRatioDropdown.value = SettingsManager.Instance.GetAspectRatio();
        }
    }

    #region Slider Handlers

    private void OnMasterVolumeChanged(float value)
    {
        if (SettingsManager.Instance != null)
        {
            SettingsManager.Instance.SetMasterVolume(value);
        }
        UpdateVolumeText(masterVolumeText, value);
    }

    private void OnMusicVolumeChanged(float value)
    {
        if (SettingsManager.Instance != null)
        {
            SettingsManager.Instance.SetMusicVolume(value);
        }
        UpdateVolumeText(musicVolumeText, value);
    }

    private void OnSFXVolumeChanged(float value)
    {
        if (SettingsManager.Instance != null)
        {
            SettingsManager.Instance.SetSFXVolume(value);
        }
        UpdateVolumeText(sfxVolumeText, value);
        
        // Mark for preview sound
        _lastSliderChangeTime = Time.time;
        _sliderWasAdjusted = true;
    }

    private void UpdateVolumeText(TextMeshProUGUI textComponent, float value)
    {
        if (textComponent != null)
        {
            textComponent.text = $"{Mathf.RoundToInt(value * 100)}%";
        }
    }

    #endregion

    #region Toggle/Dropdown Handlers

    private void OnFullscreenChanged(bool value)
    {
        PlayClickSound();
        if (SettingsManager.Instance != null)
        {
            SettingsManager.Instance.SetFullscreen(value);
        }
    }

    private void OnVSyncChanged(bool value)
    {
        PlayClickSound();
        if (SettingsManager.Instance != null)
        {
            SettingsManager.Instance.SetVSync(value);
        }
    }

    private void OnQualityChanged(int index)
    {
        PlayClickSound();
        if (SettingsManager.Instance != null)
        {
            SettingsManager.Instance.SetQuality(index);
        }
    }

    private void OnAspectRatioChanged(int index)
    {
        PlayClickSound();
        if (SettingsManager.Instance != null)
        {
            SettingsManager.Instance.SetAspectRatio(index);
        }
    }

    #endregion

    #region Button Handlers

    private void ApplySettings()
    {
        PlayClickSound();

        if (SettingsManager.Instance != null)
        {
            SettingsManager.Instance.SaveSettings();
        }

        CloseSettings();
    }

    private void ResetToDefaults()
    {
        PlayClickSound();

        if (SettingsManager.Instance != null)
        {
            SettingsManager.Instance.ResetToDefaults();
            LoadCurrentSettings();
        }
    }

    #endregion

    private void PlayClickSound()
    {
        if (uiSoundConfig != null && uiSoundConfig.buttonClick != null && AudioService.Instance != null)
        {
            AudioHelper.PlaySound(uiSoundConfig.buttonClick, AudioCategory.UI, uiSoundConfig.uiSoundVolume);
        }
    }
}
