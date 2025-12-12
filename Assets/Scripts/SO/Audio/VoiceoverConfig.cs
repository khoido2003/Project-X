using UnityEngine;

[CreateAssetMenu(fileName = "VoiceoverConfig", menuName = "Game/Audio/Voiceover Config")]
public class VoiceoverConfig : ScriptableObject
{
    [Header("Countdown Voiceovers")]
    [Tooltip("Voiceover for countdown: 3")]
    public AudioClip countdown3;

    [Tooltip("Voiceover for countdown: 2")]
    public AudioClip countdown2;

    [Tooltip("Voiceover for countdown: 1")]
    public AudioClip countdown1;

    [Tooltip("Voiceover for 'Ready'")]
    public AudioClip ready;

    [Tooltip("Voiceover for 'Game Start'")]
    public AudioClip gameStart;

    [Header("Phase Voiceovers")]
    [Tooltip("Voiceover for 'Round X' announcement")]
    public AudioClip roundAnnouncement;

    [Tooltip("Voiceover for 'Upgrade Phase'")]
    public AudioClip upgradePhase;

    [Tooltip("Voiceover for 'Combat Phase'")]
    public AudioClip combatPhase;

    [Tooltip("Voiceover for 'Boss Fight'")]
    public AudioClip bossFight;

    [Tooltip("Voiceover for 'Game Over'")]
    public AudioClip gameOver;

    [Tooltip("Voiceover for 'Victory'")]
    public AudioClip victory;

    [Tooltip("Voiceover for 'Defeat'")]
    public AudioClip defeat;

    [Header("Settings")]
    [Range(0f, 1f)]
    [Tooltip("Volume for voiceover clips")]
    public float voiceoverVolume = 0.8f;

    /// <summary>
    /// Get countdown voiceover for a specific number
    /// </summary>
    public AudioClip GetCountdownClip(int number)
    {
        return number switch
        {
            3 => countdown3,
            2 => countdown2,
            1 => countdown1,
            _ => null,
        };
    }
}
