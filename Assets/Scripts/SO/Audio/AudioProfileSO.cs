using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class AudioCueEntry
{
    public AudioCueType CueType = AudioCueType.Attack;

    [Tooltip("Category used to drive per-channel volume controls")]
    public AudioCategory Category = AudioCategory.Character;

    [Range(0f, 1f)]
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
    /// Returns a clip/category/volume tuple for a given cue type.
    /// </summary>
    public bool TryGetCue(AudioCueType cueType, out AudioClip clip, out AudioCategory category, out float volume)
    {
        category = AudioCategory.Character;
        volume = 1f;
        clip = null;

        foreach (var entry in cues)
        {
            if (entry == null || entry.CueType != cueType)
            {
                continue;
            }

            if (!entry.TryPickClip(out clip))
            {
                continue;
            }

            category = entry.Category;
            volume = entry.Volume;
            return true;
        }

        return false;
    }
}
