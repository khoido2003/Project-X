using UnityEngine;

public class AudioBootstrapper : MonoBehaviour
{
    [Header("Assign an AudioService prefab or scene instance")]
    [SerializeField]
    private AudioService audioServicePrefab;

    [Header("Optional: menu music")]
    [SerializeField]
    private AudioClip menuMusic;

    [SerializeField]
    private float menuFadeIn = 1f;

    private void Awake()
    {
        // If an AudioService already exists (from another scene), keep it
        if (AudioService.Instance != null)
        {
            // Start menu music if provided
            if (menuMusic != null)
            {
                AudioService.Instance.PlayMusic(menuMusic, menuFadeIn);
            }
            return;
        }

        // Instantiate or use scene instance
        AudioService svc = null;
        if (audioServicePrefab != null)
        {
            svc = Instantiate(audioServicePrefab);
        }
        else
        {
            // Try find existing in scene
            svc = FindFirstObjectByType<AudioService>();
            if (svc == null)
            {
                var go = new GameObject("AudioService");
                svc = go.AddComponent<AudioService>();
            }
        }

        if (svc != null && menuMusic != null)
        {
            svc.PlayMusic(menuMusic, menuFadeIn);
        }
    }
}
