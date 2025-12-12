using UnityEngine;

public static class AudioHelper
{
    /// <summary>
    /// Play a sound effect (2D) - requires World (ECS scenes)
    /// </summary>
    public static void PlaySound(World world, AudioClip clip, AudioCategory category, float? volume = null)
    {
        if (world == null || clip == null)
        {
            return;
        }

        world.Events.Publish(new PlaySoundEvent(clip, category, null, volume));
    }

    /// <summary>
    /// Play a sound effect (2D) - direct access (non-ECS scenes)
    /// </summary>
    public static void PlaySound(AudioClip clip, AudioCategory category, float? volume = null)
    {
        if (clip == null || AudioService.Instance == null)
        {
            return;
        }

        AudioService.Instance.PlaySound(clip, category, null, volume);
    }

    /// <summary>
    /// Play a 3D sound effect at a world position - requires World (ECS scenes)
    /// </summary>
    public static void PlaySound3D(
        World world,
        AudioClip clip,
        AudioCategory category,
        Vector3 position,
        float? volume = null
    )
    {
        if (world == null || clip == null)
        {
            return;
        }

        world.Events.Publish(new PlaySoundEvent(clip, category, position, volume));
    }

    /// <summary>
    /// Play a 3D sound effect at a world position - direct access (non-ECS scenes)
    /// </summary>
    public static void PlaySound3D(AudioClip clip, AudioCategory category, Vector3 position, float? volume = null)
    {
        if (clip == null || AudioService.Instance == null)
        {
            return;
        }

        AudioService.Instance.PlaySound(clip, category, position, volume);
    }

    /// <summary>
    /// Play background music - requires World (ECS scenes)
    /// </summary>
    public static void PlayMusic(World world, AudioClip clip, float fadeIn = 1f)
    {
        if (world == null || clip == null)
        {
            return;
        }

        world.Events.Publish(new PlayMusicEvent(clip, fadeIn));
    }

    /// <summary>
    /// Play background music - direct access (non-ECS scenes)
    /// </summary>
    public static void PlayMusic(AudioClip clip, float fadeIn = 1f)
    {
        if (clip == null || AudioService.Instance == null)
        {
            return;
        }

        AudioService.Instance.PlayMusic(clip, fadeIn);
    }

    /// <summary>
    /// Stop background music - requires World (ECS scenes)
    /// </summary>
    public static void StopMusic(World world, float fadeOut = 1f)
    {
        if (world == null)
        {
            return;
        }

        world.Events.Publish(new StopMusicEvent(fadeOut));
    }

    /// <summary>
    /// Stop background music - direct access (non-ECS scenes)
    /// </summary>
    public static void StopMusic(float fadeOut = 1f)
    {
        if (AudioService.Instance == null)
        {
            return;
        }

        AudioService.Instance.StopMusic(fadeOut);
    }

    /// <summary>
    /// Set volume for an audio category - requires World (ECS scenes)
    /// </summary>
    public static void SetVolume(World world, AudioCategory category, float volume)
    {
        if (world == null)
        {
            return;
        }

        world.Events.Publish(new SetVolumeEvent(category, volume));
    }

    /// <summary>
    /// Set volume for an audio category - direct access (non-ECS scenes)
    /// </summary>
    public static void SetVolume(AudioCategory category, float volume)
    {
        if (AudioService.Instance == null)
        {
            return;
        }

        AudioService.Instance.SetCategoryVolume(category, volume);
    }
}
