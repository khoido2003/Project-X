using UnityEngine;

[CreateAssetMenu(fileName = "SceneAudioConfig", menuName = "Game/Audio/Scene Audio Config")]
public class SceneAudioConfig : ScriptableObject
{
    [Header("Scene Music")]
    [Tooltip("Background music for Menu scene")]
    public AudioClip menuMusic;

    [Tooltip("Background music for Character Selection scene")]
    public AudioClip characterSelectionMusic;

    [Tooltip("Background music for Map_1 scene")]
    public AudioClip map1Music;

    [Tooltip("Background music for Map_2 scene")]
    public AudioClip map2Music;

    [Tooltip("Background music for Map_3 scene")]
    public AudioClip map3Music;

    [Tooltip("Background music for Victory scene")]
    public AudioClip victoryMusic;

    [Tooltip("Background music for Defeat scene")]
    public AudioClip defeatMusic;

    [Header("Music Settings")]
    [Range(0f, 2f)]
    [Tooltip("Fade in time for music transitions")]
    public float musicFadeInTime = 1f;

    [Range(0f, 2f)]
    [Tooltip("Fade out time for music transitions")]
    public float musicFadeOutTime = 1f;

    /// <summary>
    /// Get music clip for a specific scene
    /// </summary>
    public AudioClip GetMusicForScene(SceneName sceneName)
    {
        return sceneName switch
        {
            SceneName.Menu => menuMusic,
            SceneName.CharacterSelection => characterSelectionMusic,
            SceneName.Map_1 => map1Music,
            SceneName.Map_2 => map2Music,
            SceneName.Map_3 => map3Music,
            SceneName.Victory => victoryMusic,
            SceneName.Defeat => defeatMusic,
            _ => null,
        };
    }
}

