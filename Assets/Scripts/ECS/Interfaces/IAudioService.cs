using UnityEngine;

public interface IAudioService
{
    void PlaySound(AudioClip clip, AudioCategory category, Vector3? position = null, float? volume = null);

    void PlayMusic(AudioClip clip, float fadeIn = 1f);

    void StopMusic(float fadeOut = 1f);

    void SetCategoryVolume(AudioCategory category, float volume);

    float GetCategoryVolume(AudioCategory category);

    void SetMasterVolume(float volume);

    float GetMasterVolume();
}
