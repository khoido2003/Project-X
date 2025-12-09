using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Audio system that handles audio events and plays sounds through the audio service.
/// Subscribes to audio events and delegates to IAudioService.
/// </summary>
public class AudioSystem : ISystem
{
    private World _world;
    private IAudioService _audioService;

    public void Initialize(World world)
    {
        _world = world;
        _audioService = world.Services.Resolve<IAudioService>();

        // Subscribe to audio events
        _world.Events.Subscribe<PlaySoundEvent>(OnPlaySound);
        _world.Events.Subscribe<PlayMusicEvent>(OnPlayMusic);
        _world.Events.Subscribe<StopMusicEvent>(OnStopMusic);
        _world.Events.Subscribe<SetVolumeEvent>(OnSetVolume);
    }

    public void Update(float dt) { }

    public void FixedUpdate(float dt) { }

    private void OnPlaySound(PlaySoundEvent @event)
    {
        if (@event.Clip == null)
        {
            return;
        }

        if (@event.IsLooping)
        {
            // For looping sounds, you might want special handling
            // For now, just play as normal
        }

        _audioService.PlaySound(@event.Clip, @event.Category, @event.Position, @event.Volume);
    }

    private void OnPlayMusic(PlayMusicEvent @event)
    {
        if (@event.Clip == null)
        {
            return;
        }

        _audioService.PlayMusic(@event.Clip, @event.FadeIn);
    }

    private void OnStopMusic(StopMusicEvent @event)
    {
        _audioService.StopMusic(@event.FadeOut);
    }

    private void OnSetVolume(SetVolumeEvent @event)
    {
        _audioService.SetCategoryVolume(@event.Category, @event.Volume);
    }

    public void Shutdown()
    {
        _world.Events.Unsubscribe<PlaySoundEvent>(OnPlaySound);
        _world.Events.Unsubscribe<PlayMusicEvent>(OnPlayMusic);
        _world.Events.Unsubscribe<StopMusicEvent>(OnStopMusic);
        _world.Events.Unsubscribe<SetVolumeEvent>(OnSetVolume);
    }
}

