using UnityEngine;

[CreateAssetMenu(fileName = "UISoundConfig", menuName = "Game/Audio/UI Sound Config")]
public class UISoundConfig : ScriptableObject
{
    [Header("Button Sounds")]
    [Tooltip("Sound for button click/confirm")]
    public AudioClip buttonClick;

    [Tooltip("Sound for button cancel/back")]
    public AudioClip buttonCancel;

    [Tooltip("Sound for button hover")]
    public AudioClip buttonHover;

    [Header("Upgrade System")]
    [Tooltip("Sound when upgrade card appears")]
    public AudioClip upgradeCardAppear;

    [Tooltip("Sound when upgrade is selected")]
    public AudioClip upgradeSelected;

    [Header("Settings")]
    [Range(0f, 1f)]
    [Tooltip("Volume for UI sounds")]
    public float uiSoundVolume = 0.7f;
}
