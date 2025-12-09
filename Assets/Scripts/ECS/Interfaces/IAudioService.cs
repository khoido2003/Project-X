using UnityEngine;

/// <summary>
/// Audio service interface for playing sounds and music throughout the game.
/// Supports different audio categories with independent volume controls.
/// </summary>
public interface IAudioService
{
    /// <summary>
    /// Play a one-shot sound effect (UI, weapon, skill, etc.)
    /// </summary>
    /// <param name="clip">Audio clip to play</param>
    /// <param name="category">Audio category for volume control</param>
    /// <param name="position">World position (for 3D sounds), null for 2D</param>
    /// <param name="volume">Volume multiplier (0-1), uses category volume if null</param>
    void PlaySound(AudioClip clip, AudioCategory category, Vector3? position = null, float? volume = null);

    /// <summary>
    /// Play background music (loops automatically)
    /// </summary>
    /// <param name="clip">Music clip to play</param>
    /// <param name="fadeIn">Fade in duration in seconds</param>
    void PlayMusic(AudioClip clip, float fadeIn = 1f);

    /// <summary>
    /// Stop background music
    /// </summary>
    /// <param name="fadeOut">Fade out duration in seconds</param>
    void StopMusic(float fadeOut = 1f);

    /// <summary>
    /// Set volume for a specific audio category
    /// </summary>
    /// <param name="category">Audio category</param>
    /// <param name="volume">Volume (0-1)</param>
    void SetCategoryVolume(AudioCategory category, float volume);

    /// <summary>
    /// Get volume for a specific audio category
    /// </summary>
    float GetCategoryVolume(AudioCategory category);

    /// <summary>
    /// Set master volume (affects all sounds)
    /// </summary>
    void SetMasterVolume(float volume);

    /// <summary>
    /// Get master volume
    /// </summary>
    float GetMasterVolume();
}

