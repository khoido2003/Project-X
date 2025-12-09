using UnityEngine;

/// <summary>
/// Audio category for volume control and organization
/// </summary>
public enum AudioCategory
{
    Master,
    Music,
    UI,
    Character,
    Weapon,
    Skill,
    Enemy,
    Environment,
    Footstep,
}

/// <summary>
/// Event to play a sound effect
/// </summary>
public struct PlaySoundEvent
{
    public AudioClip Clip;
    public AudioCategory Category;
    public Vector3? Position; // null for 2D, Vector3 for 3D
    public float? Volume; // null to use category volume
    public bool IsLooping;

    public PlaySoundEvent(AudioClip clip, AudioCategory category, Vector3? position = null, float? volume = null, bool isLooping = false)
    {
        Clip = clip;
        Category = category;
        Position = position;
        Volume = volume;
        IsLooping = isLooping;
    }
}

/// <summary>
/// Event to play background music
/// </summary>
public struct PlayMusicEvent
{
    public AudioClip Clip;
    public float FadeIn;

    public PlayMusicEvent(AudioClip clip, float fadeIn = 1f)
    {
        Clip = clip;
        FadeIn = fadeIn;
    }
}

/// <summary>
/// Event to stop background music
/// </summary>
public struct StopMusicEvent
{
    public float FadeOut;

    public StopMusicEvent(float fadeOut = 1f)
    {
        FadeOut = fadeOut;
    }
}

/// <summary>
/// Event to set volume for an audio category
/// </summary>
public struct SetVolumeEvent
{
    public AudioCategory Category;
    public float Volume;

    public SetVolumeEvent(AudioCategory category, float volume)
    {
        Category = category;
        Volume = volume;
    }
}

