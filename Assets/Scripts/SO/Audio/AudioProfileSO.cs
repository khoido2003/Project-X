using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class AudioCueEntry
{
    public SoundType SoundType = SoundType.Attack;

    [Range(0f, 1f)]
    [Tooltip("Volume multiplier for this specific sound (0-1)")]
    public float Volume = 1f;

    [Tooltip("One will be picked at random. Leave empty to skip this cue.")]
    public AudioClip[] Clips;

    public bool TryPickClip(out AudioClip clip)
    {
        clip = null;
        if (Clips == null || Clips.Length == 0)
        {
            return false;
        }
        int idx = UnityEngine.Random.Range(0, Clips.Length);
        clip = Clips[idx];
        return clip != null;
    }
}

[CreateAssetMenu(menuName = "Game/Audio/Audio Profile", fileName = "AudioProfile")]
public class AudioProfileSO : ScriptableObject
{
    [SerializeField]
    private List<AudioCueEntry> cues = new();

    /// <summary>
    /// Returns a clip and volume for a given sound type.
    /// Category is determined by the entity type (Player/Enemy) automatically.
    /// </summary>
    public bool TryGetCue(SoundType soundType, out AudioClip clip, out float volume)
    {
        volume = 1f;
        clip = null;

        foreach (var entry in cues)
        {
            if (entry == null || entry.SoundType != soundType)
            {
                continue;
            }

            if (!entry.TryPickClip(out clip))
            {
                continue;
            }

            volume = entry.Volume;
            return true;
        }

        return false;
    }
}
