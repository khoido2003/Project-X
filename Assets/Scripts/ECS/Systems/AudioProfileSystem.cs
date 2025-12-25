using UnityEngine;

/// <summary>
/// Plays entity-based audio cues.
/// Automatically determines if entity is Player or Enemy for volume control.
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
        // Get audio profile
        if (!_world.Components.TryGet(@event.Entity, out AudioProfileComponent profile))
        {
            Debug.LogWarning(
                $"[AudioProfileSystem] Entity {@event.Entity.Id} missing AudioProfileComponent. SoundType: {@event.SoundType}. Make sure the entity was created through a Factory."
            );
            return;
        }

        if (profile.Profile == null)
        {
            // Client-side enemies may have null profiles since we don't sync audio profiles over network
            // Server handles enemy audio playback - silently skip on client
            if (_world.Components.Has<EnemyComponent>(@event.Entity))
            {
                return; // Silent skip for enemies - server handles their audio
            }
            
            // For players, log a warning since they should have profiles from CharacterData
            string entityName = $"Entity {@event.Entity.Id}";
            if (_world.Components.TryGet(@event.Entity, out CharacterSelectionComponent charSel))
            {
                entityName = charSel.CharacterData?.characterName ?? entityName;
            }

            Debug.LogWarning(
                $"[AudioProfileSystem] {entityName} has AudioProfileComponent but Profile is null. SoundType: {@event.SoundType}. " +
                $"Please assign an AudioProfileSO to the entity's DefinitionSO (CharacterDefinitionSO or EnemyDefinitionSO)."
            );
            return;
        }

        // Get sound clip from profile
        if (!profile.Profile.TryGetCue(@event.SoundType, out AudioClip clip, out float baseVolume))
        {
            Debug.LogWarning(
                $"[AudioProfileSystem] AudioProfile '{profile.Profile.name}' does not have a cue for SoundType: {@event.SoundType} (Entity: {@event.Entity.Id})"
            );
            return;
        }

        if (clip == null)
        {
            Debug.LogWarning(
                $"[AudioProfileSystem] AudioProfile '{profile.Profile.name}' returned null clip for SoundType: {@event.SoundType}"
            );
            return;
        }

        // Get position
        if (!_world.Components.TryGet(@event.Entity, out TransformComponent transform))
        {
            Debug.LogWarning(
                $"[AudioProfileSystem] Entity {@event.Entity.Id} missing TransformComponent for sound playback"
            );
            return;
        }

        Vector3 position = @event.PositionOverride ?? transform.Position;
        float? volume = @event.VolumeOverride ?? baseVolume;

        // Determine category based on entity type
        AudioCategory category = DetermineAudioCategory(@event.Entity);

        // Play sound
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

    private AudioCategory DetermineAudioCategory(EntityId entity)
    {
        if (_world.Components.Has<PlayerTagComponent>(entity))
        {
            return AudioCategory.Player;
        }

        if (_world.Components.Has<EnemyComponent>(entity))
        {
            return AudioCategory.Enemy;
        }

        // Default fallback
        return AudioCategory.Environment;
    }

    public void Shutdown()
    {
        _world.Events.Unsubscribe<AudioCueEvent>(OnAudioCueEvent);
    }
}
