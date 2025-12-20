using UnityEngine;

public enum AudioCategory
{
    Master,
    Music,
    UI,
    Player, // All player sounds (attacks, skills, footsteps, etc.)
    Enemy, // All enemy sounds (attacks, footsteps, death, etc.)
    Environment, // Ambient sounds, environmental effects
}

public enum SoundType
{
    Attack,
    Skill,
    Impact, // Getting hit
    Footstep,
    Death,
    Spawn,
    // Add more as needed
}

/// <summary>
/// Event to play a sound effect.
/// </summary>
public struct PlaySoundEvent
{
    public AudioClip Clip;
    public AudioCategory Category;
    public Vector3? Position;
    public float? Volume;
    public bool IsLooping;

    public PlaySoundEvent(
        AudioClip clip,
        AudioCategory category,
        Vector3? position = null,
        float? volume = null,
        bool isLooping = false
    )
    {
        Clip = clip;
        Category = category;
        Position = position;
        Volume = volume;
        IsLooping = isLooping;
    }
}

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

public struct StopMusicEvent
{
    public float FadeOut;

    public StopMusicEvent(float fadeOut = 1f)
    {
        FadeOut = fadeOut;
    }
}

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

public struct AudioCueEvent
{
    public EntityId Entity;
    public SoundType SoundType;
    public Vector3? PositionOverride;
    public float? VolumeOverride;

    public AudioCueEvent(
        EntityId entity,
        SoundType soundType,
        Vector3? positionOverride = null,
        float? volumeOverride = null
    )
    {
        Entity = entity;
        SoundType = soundType;
        PositionOverride = positionOverride;
        VolumeOverride = volumeOverride;
    }
}
