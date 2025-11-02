using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SettingWindow : MonoBehaviour
{
    [Header("Main References")]
    public GameObject homeWindow;
    public GameObject settingPanel;
    public Button settingButton;

    [Header("Setting Sections")]
    public GameObject audioOptions;
    public GameObject controlOptions;

    [Header("Navigation Buttons")]
    public Button audioButton;
    public Button controlButton;
    public Button logoutButton;
    public Button exitButton;
    public Button quitButton;

    [Header("Audio Settings")]
    public Slider masterVolumeSlider;
    public Toggle masterVolumeToggle;
    public Slider musicSlider;
    public Toggle musicToggle;
    public Slider sfxSlider;
    public Toggle sfxToggle;

    private void Start()
    {
        Debug.Log("SettingWindow Start called");
        InitializeEventListeners();
    }

    private void InitializeEventListeners()
    {
        Debug.Log("Initializing event listeners only");

        if (settingButton != null)
        {
            settingButton.onClick.RemoveAllListeners();
            settingButton.onClick.AddListener(OpenSetting);
        }

        if (exitButton != null)
        {
            exitButton.onClick.RemoveAllListeners();
            exitButton.onClick.AddListener(CloseSetting);
        }

        if (audioButton != null)
        {
            audioButton.onClick.RemoveAllListeners();
            audioButton.onClick.AddListener(ShowAudioOptions);
        }

        if (controlButton != null)
        {
            controlButton.onClick.RemoveAllListeners();
            controlButton.onClick.AddListener(ShowControlOptions);
        }

        if (logoutButton != null)
        {
            logoutButton.onClick.RemoveAllListeners();
            logoutButton.onClick.AddListener(Logout);
        }


        if (quitButton != null)
        {
            quitButton.onClick.RemoveAllListeners();
            quitButton.onClick.AddListener(QuitGame);
            Debug.Log("Quit button event assigned");
        }

        InitializeSettings();
    }

    public void OpenSetting()
    {
        Debug.Log("Opening setting panel");

        if (homeWindow != null)
            homeWindow.SetActive(false);

        if (settingPanel != null)
            settingPanel.SetActive(true);

        ShowAudioOptions();
    }

    public void CloseSetting()
    {
        Debug.Log("Closing setting panel");

        if (settingPanel != null)
            settingPanel.SetActive(false);

        if (homeWindow != null)
            homeWindow.SetActive(true);
    }


    public void ShowAudioOptions()
    {
        Debug.Log("Showing Audio Options");

        if (audioOptions == null)
        {
            Debug.LogError("Audio Options is null!");
            return;
        }

        if (controlOptions != null)
            controlOptions.SetActive(false);

        audioOptions.SetActive(true);

        SetButtonActiveState(audioButton, true);
        SetButtonActiveState(controlButton, false);
    }


    public void ShowControlOptions()
    {
        Debug.Log("Showing Control Options");

        if (controlOptions == null)
        {
            Debug.LogError("Control Options is null!");
            return;
        }

        if (audioOptions != null)
            audioOptions.SetActive(false);

        controlOptions.SetActive(true);

        SetButtonActiveState(audioButton, false);
        SetButtonActiveState(controlButton, true);
    }


    private void SetButtonActiveState(Button button, bool isActive)
    {
        if (button == null) return;

        ColorBlock colors = button.colors;

        if (isActive)
        {
            colors.normalColor = new Color(0.2f, 0.6f, 1f);
            colors.selectedColor = new Color(0.2f, 0.6f, 1f);
            colors.highlightedColor = new Color(0.3f, 0.7f, 1f);
            colors.pressedColor = new Color(0.1f, 0.5f, 0.9f);
        }
        else
        {
            colors.normalColor = Color.white;
            colors.selectedColor = Color.white;
            colors.highlightedColor = new Color(0.9f, 0.9f, 0.9f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
        }

        button.colors = colors;

        Text buttonText = button.GetComponentInChildren<Text>();
        if (buttonText != null)
        {
            buttonText.color = isActive ? new Color(0.2f, 0.6f, 1f) : Color.black;
        }

        TextMeshProUGUI buttonTMP = button.GetComponentInChildren<TextMeshProUGUI>();
        if (buttonTMP != null)
        {
            buttonTMP.color = isActive ? new Color(0.2f, 0.6f, 1f) : Color.black;
        }

        button.transform.localScale = isActive ? new Vector3(1.05f, 1.05f, 1f) : Vector3.one;
    }


    public void Logout()
    {
        Debug.Log("Logout clicked");

    }


    public void QuitGame()
    {
        Debug.Log("Quit game clicked");

#if UNITY_EDITOR
        
        UnityEditor.EditorApplication.isPlaying = false;
#else

        Application.Quit();
#endif
    }


    public void QuitWithConfirmation()
    {

        Debug.Log("Show quit confirmation dialog");


        QuitGame();
    }

    private void InitializeSettings()
    {
        if (masterVolumeSlider != null)
        {
            float savedVolume = PlayerPrefs.GetFloat("MasterVolume", 1f);
            masterVolumeSlider.SetValueWithoutNotify(savedVolume);
            masterVolumeSlider.onValueChanged.AddListener(SetMasterVolume);
        }

        if (masterVolumeToggle != null)
        {
            bool savedMute = PlayerPrefs.GetInt("MasterMute", 0) == 1;
            masterVolumeToggle.SetIsOnWithoutNotify(savedMute);
            masterVolumeToggle.onValueChanged.AddListener(ToggleMasterVolume);
        }

        if (musicSlider != null)
        {
            float savedMusicVolume = PlayerPrefs.GetFloat("MusicVolume", 1f);
            musicSlider.SetValueWithoutNotify(savedMusicVolume);
            musicSlider.onValueChanged.AddListener(SetMusicVolume);
        }

        if (musicToggle != null)
        {
            bool savedMusicMute = PlayerPrefs.GetInt("MusicMute", 0) == 1;
            musicToggle.SetIsOnWithoutNotify(savedMusicMute);
            musicToggle.onValueChanged.AddListener(ToggleMusic);
        }

        if (sfxSlider != null)
        {
            float savedSFXVolume = PlayerPrefs.GetFloat("SFXVolume", 1f);
            sfxSlider.SetValueWithoutNotify(savedSFXVolume);
            sfxSlider.onValueChanged.AddListener(SetSFXVolume);
        }

        if (sfxToggle != null)
        {
            bool savedSFXMute = PlayerPrefs.GetInt("SFXMute", 0) == 1;
            sfxToggle.SetIsOnWithoutNotify(savedSFXMute);
            sfxToggle.onValueChanged.AddListener(ToggleSFX);
        }
    }

    private void SetMasterVolume(float volume)
    {
        AudioListener.volume = volume;
        PlayerPrefs.SetFloat("MasterVolume", volume);
    }

    private void ToggleMasterVolume(bool isMuted)
    {
        AudioListener.volume = isMuted ? 0f : masterVolumeSlider.value;
        PlayerPrefs.SetInt("MasterMute", isMuted ? 1 : 0);
    }

    private void SetMusicVolume(float volume)
    {
        PlayerPrefs.SetFloat("MusicVolume", volume);
    }

    private void ToggleMusic(bool isMuted)
    {
        PlayerPrefs.SetInt("MusicMute", isMuted ? 1 : 0);
    }

    private void SetSFXVolume(float volume)
    {
        PlayerPrefs.SetFloat("SFXVolume", volume);
    }

    private void ToggleSFX(bool isMuted)
    {
        PlayerPrefs.SetInt("SFXMute", isMuted ? 1 : 0);
    }
}