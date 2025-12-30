using UnityEngine;

/// <summary>
/// Custom quality presets for graphics settings.
/// </summary>
public enum GraphicsQuality
{
    Low = 0,
    Medium = 1,
    High = 2
}

/// <summary>
/// Manages game settings and persists them using PlayerPrefs.
/// </summary>
public class SettingsManager : MonoBehaviour
{
    public static SettingsManager Instance { get; private set; }

    // Custom quality names
    private static readonly string[] QUALITY_NAMES = { "Low", "Medium", "High" };

    // PlayerPrefs keys
    private const string KEY_MASTER_VOLUME = "Settings_MasterVolume";
    private const string KEY_MUSIC_VOLUME = "Settings_MusicVolume";
    private const string KEY_SFX_VOLUME = "Settings_SFXVolume";
    private const string KEY_FULLSCREEN = "Settings_Fullscreen";
    private const string KEY_VSYNC = "Settings_VSync";
    private const string KEY_QUALITY = "Settings_Quality";

    // Default values
    private const float DEFAULT_MASTER_VOLUME = 1f;
    private const float DEFAULT_MUSIC_VOLUME = 0.5f;
    private const float DEFAULT_SFX_VOLUME = 1f;
    private const int DEFAULT_QUALITY = (int)GraphicsQuality.High;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadSettings();
    }

    /// <summary>
    /// Load settings from PlayerPrefs and apply them.
    /// </summary>
    public void LoadSettings()
    {
        // Load volume settings
        float masterVolume = PlayerPrefs.GetFloat(KEY_MASTER_VOLUME, DEFAULT_MASTER_VOLUME);
        float musicVolume = PlayerPrefs.GetFloat(KEY_MUSIC_VOLUME, DEFAULT_MUSIC_VOLUME);
        float sfxVolume = PlayerPrefs.GetFloat(KEY_SFX_VOLUME, DEFAULT_SFX_VOLUME);

        // Load display settings
        bool fullscreen = PlayerPrefs.GetInt(KEY_FULLSCREEN, Screen.fullScreen ? 1 : 0) == 1;
        bool vsync = PlayerPrefs.GetInt(KEY_VSYNC, QualitySettings.vSyncCount > 0 ? 1 : 0) == 1;
        int quality = PlayerPrefs.GetInt(KEY_QUALITY, QualitySettings.GetQualityLevel());

        // Apply settings
        ApplyMasterVolume(masterVolume);
        ApplyMusicVolume(musicVolume);
        ApplySFXVolume(sfxVolume);
        ApplyFullscreen(fullscreen);
        ApplyVSync(vsync);
        ApplyQuality(quality);
    }

    /// <summary>
    /// Save all current settings to PlayerPrefs.
    /// </summary>
    public void SaveSettings()
    {
        PlayerPrefs.Save();
    }

    #region Volume Settings

    public float GetMasterVolume()
    {
        return PlayerPrefs.GetFloat(KEY_MASTER_VOLUME, DEFAULT_MASTER_VOLUME);
    }

    public void SetMasterVolume(float volume)
    {
        volume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat(KEY_MASTER_VOLUME, volume);
        ApplyMasterVolume(volume);
    }

    private void ApplyMasterVolume(float volume)
    {
        if (AudioService.Instance != null)
        {
            AudioService.Instance.SetCategoryVolume(AudioCategory.Master, volume);
        }
    }

    public float GetMusicVolume()
    {
        return PlayerPrefs.GetFloat(KEY_MUSIC_VOLUME, DEFAULT_MUSIC_VOLUME);
    }

    public void SetMusicVolume(float volume)
    {
        volume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat(KEY_MUSIC_VOLUME, volume);
        ApplyMusicVolume(volume);
    }

    private void ApplyMusicVolume(float volume)
    {
        if (AudioService.Instance != null)
        {
            AudioService.Instance.SetCategoryVolume(AudioCategory.Music, volume);
        }
    }

    public float GetSFXVolume()
    {
        return PlayerPrefs.GetFloat(KEY_SFX_VOLUME, DEFAULT_SFX_VOLUME);
    }

    public void SetSFXVolume(float volume)
    {
        volume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat(KEY_SFX_VOLUME, volume);
        ApplySFXVolume(volume);
    }

    private void ApplySFXVolume(float volume)
    {
        if (AudioService.Instance != null)
        {
            // Apply to all SFX categories
            AudioService.Instance.SetCategoryVolume(AudioCategory.UI, volume);
            AudioService.Instance.SetCategoryVolume(AudioCategory.Player, volume);
            AudioService.Instance.SetCategoryVolume(AudioCategory.Enemy, volume);
            AudioService.Instance.SetCategoryVolume(AudioCategory.Environment, volume);
        }
    }

    #endregion

    #region Display Settings

    public bool GetFullscreen()
    {
        return PlayerPrefs.GetInt(KEY_FULLSCREEN, Screen.fullScreen ? 1 : 0) == 1;
    }

    public void SetFullscreen(bool fullscreen)
    {
        PlayerPrefs.SetInt(KEY_FULLSCREEN, fullscreen ? 1 : 0);
        ApplyFullscreen(fullscreen);
    }

    private void ApplyFullscreen(bool fullscreen)
    {
        Screen.fullScreen = fullscreen;
    }

    public bool GetVSync()
    {
        return PlayerPrefs.GetInt(KEY_VSYNC, QualitySettings.vSyncCount > 0 ? 1 : 0) == 1;
    }

    public void SetVSync(bool vsync)
    {
        PlayerPrefs.SetInt(KEY_VSYNC, vsync ? 1 : 0);
        ApplyVSync(vsync);
    }

    private void ApplyVSync(bool vsync)
    {
        QualitySettings.vSyncCount = vsync ? 1 : 0;
    }

    public int GetQuality()
    {
        return PlayerPrefs.GetInt(KEY_QUALITY, DEFAULT_QUALITY);
    }

    public void SetQuality(int qualityIndex)
    {
        qualityIndex = Mathf.Clamp(qualityIndex, 0, QUALITY_NAMES.Length - 1);
        PlayerPrefs.SetInt(KEY_QUALITY, qualityIndex);
        ApplyQuality(qualityIndex);
    }

    private void ApplyQuality(int qualityIndex)
    {
        GraphicsQuality quality = (GraphicsQuality)qualityIndex;
        
        switch (quality)
        {
            case GraphicsQuality.Low:
                ApplyLowQualitySettings();
                break;
            case GraphicsQuality.Medium:
                ApplyMediumQualitySettings();
                break;
            case GraphicsQuality.High:
            default:
                ApplyHighQualitySettings();
                break;
        }
    }
    
    private void ApplyLowQualitySettings()
    {
        // Shadows - disabled or minimal
        QualitySettings.shadows = ShadowQuality.Disable;
        QualitySettings.shadowResolution = ShadowResolution.Low;
        QualitySettings.shadowDistance = 20f;
        
        // LOD and draw distance
        QualitySettings.lodBias = 0.5f;
        QualitySettings.maximumLODLevel = 2;
        
        // Particles and effects
        QualitySettings.softParticles = false;
        QualitySettings.particleRaycastBudget = 64;
        
        // Anti-aliasing
        QualitySettings.antiAliasing = 0;
        
        // Textures
        QualitySettings.globalTextureMipmapLimit = 2; // Quarter resolution
        
        // Real-time reflection probes
        QualitySettings.realtimeReflectionProbes = false;
    }
    
    private void ApplyMediumQualitySettings()
    {
        // Shadows - medium quality
        QualitySettings.shadows = ShadowQuality.HardOnly;
        QualitySettings.shadowResolution = ShadowResolution.Medium;
        QualitySettings.shadowDistance = 50f;
        
        // LOD and draw distance
        QualitySettings.lodBias = 1f;
        QualitySettings.maximumLODLevel = 1;
        
        // Particles and effects
        QualitySettings.softParticles = true;
        QualitySettings.particleRaycastBudget = 256;
        
        // Anti-aliasing - 2x
        QualitySettings.antiAliasing = 2;
        
        // Textures
        QualitySettings.globalTextureMipmapLimit = 1; // Half resolution
        
        // Real-time reflection probes
        QualitySettings.realtimeReflectionProbes = true;
    }
    
    private void ApplyHighQualitySettings()
    {
        // Shadows - high quality
        QualitySettings.shadows = ShadowQuality.All;
        QualitySettings.shadowResolution = ShadowResolution.VeryHigh;
        QualitySettings.shadowDistance = 100f;
        
        // LOD and draw distance
        QualitySettings.lodBias = 2f;
        QualitySettings.maximumLODLevel = 0;
        
        // Particles and effects
        QualitySettings.softParticles = true;
        QualitySettings.particleRaycastBudget = 1024;
        
        // Anti-aliasing - 4x or 8x
        QualitySettings.antiAliasing = 4;
        
        // Textures
        QualitySettings.globalTextureMipmapLimit = 0; // Full resolution
        
        // Real-time reflection probes
        QualitySettings.realtimeReflectionProbes = true;
    }

    public string[] GetQualityNames()
    {
        return QUALITY_NAMES;
    }

    #endregion

    /// <summary>
    /// Reset all settings to default values.
    /// </summary>
    public void ResetToDefaults()
    {
        SetMasterVolume(DEFAULT_MASTER_VOLUME);
        SetMusicVolume(DEFAULT_MUSIC_VOLUME);
        SetSFXVolume(DEFAULT_SFX_VOLUME);
        SetFullscreen(true);
        SetVSync(true);
        SetQuality(QualitySettings.names.Length - 1); // Highest quality
        SaveSettings();
    }
}
