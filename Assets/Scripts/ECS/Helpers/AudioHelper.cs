using UnityEngine;

public static class AudioHelper
{
    /// <summary>
    /// Play a sound effect (2D)
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
    /// Play a 3D sound effect at a world position
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
    /// Play background music
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
    /// Stop background music
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
    /// Set volume for an audio category
    /// </summary>
    public static void SetVolume(World world, AudioCategory category, float volume)
    {
        if (world == null)
        {
            return;
        }

        world.Events.Publish(new SetVolumeEvent(category, volume));
    }
}
