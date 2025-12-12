using UnityEngine;

/// <summary>
/// Plays simple per-entity cues (spawn/attack/skill/impact/footstep/death) from AudioProfileSO.
/// </summary>
public class AudioProfileSystem : ISystem
{
    private World _world;

    public void Initialize(World world)
    {
        _world = world;
        _world.Events.Subscribe<AudioCueEvent>(OnAudioCueEvent);
    }

    public void Update(float dt) { }

    public void FixedUpdate(float dt) { }

    private void OnAudioCueEvent(AudioCueEvent @event)
    {
        if (!_world.Components.TryGet(@event.Entity, out AudioProfileComponent profile) || profile.Profile == null)
        {
            return;
        }

        if (
            !profile.Profile.TryGetCue(
                @event.CueType,
                out AudioClip clip,
                out AudioCategory category,
                out float baseVolume
            )
        )
        {
            return;
        }

        if (!_world.Components.TryGet(@event.Entity, out TransformComponent transform))
        {
            return;
        }

        Vector3 position = @event.PositionOverride ?? transform.Position;
        float? volume = @event.VolumeOverride ?? baseVolume;

        // For footstep sounds, pass entity ID so we can stop them later
        if (@event.CueType == AudioCueType.Footstep)
        {
            var audioService = _world.Services.Resolve<IAudioService>();
            if (audioService is AudioService service)
            {
                service.PlaySoundForEntity(clip, category, position, volume, @event.Entity);
            }
            else
            {
                AudioHelper.PlaySound3D(_world, clip, category, position, volume);
            }
        }
        else
        {
            AudioHelper.PlaySound3D(_world, clip, category, position, volume);
        }
    }

    public void Shutdown()
    {
        _world.Events.Unsubscribe<AudioCueEvent>(OnAudioCueEvent);
    }
}
