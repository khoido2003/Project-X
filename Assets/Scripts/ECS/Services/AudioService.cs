using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AudioService : MonoBehaviour, IAudioService
{
    public static AudioService Instance { get; private set; }

    [Header("Audio Sources")]
    [SerializeField]
    private AudioSource musicSource;

    [SerializeField]
    private int soundSourcePoolSize = 10;

    [SerializeField]
    [Range(0f, 1f)]
    private float masterVolume = 1f;

    [SerializeField]
    [Range(0f, 1f)]
    private float musicVolume = 0.7f;

    [SerializeField]
    [Range(0f, 1f)]
    private float uiVolume = 1f;

    [SerializeField]
    [Range(0f, 1f)]
    private float characterVolume = 1f;

    [SerializeField]
    [Range(0f, 1f)]
    private float weaponVolume = 1f;

    [SerializeField]
    [Range(0f, 1f)]
    private float skillVolume = 1f;

    [SerializeField]
    [Range(0f, 1f)]
    private float enemyVolume = 1f;

    [SerializeField]
    [Range(0f, 1f)]
    private float environmentVolume = 1f;

    [SerializeField]
    [Range(0f, 1f)]
    private float footstepVolume = 0.8f;

    [SerializeField]
    private float minDistance3D = 1f;

    [SerializeField]
    private float maxDistance3D = 50f;

    private Queue<AudioSource> _availableSources = new();
    private List<AudioSource> _activeSources = new();
    private Dictionary<AudioCategory, float> _categoryVolumes = new();
    private AudioSource _currentMusicSource;
    private Coroutine _musicFadeCoroutine;

    private void Awake()
    {
        // Singleton + persist across scenes (menus/maps share one audio service)
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        _categoryVolumes[AudioCategory.Master] = masterVolume;
        _categoryVolumes[AudioCategory.Music] = musicVolume;
        _categoryVolumes[AudioCategory.UI] = uiVolume;
        _categoryVolumes[AudioCategory.Character] = characterVolume;
        _categoryVolumes[AudioCategory.Weapon] = weaponVolume;
        _categoryVolumes[AudioCategory.Skill] = skillVolume;
        _categoryVolumes[AudioCategory.Enemy] = enemyVolume;
        _categoryVolumes[AudioCategory.Environment] = environmentVolume;
        _categoryVolumes[AudioCategory.Footstep] = footstepVolume;

        if (musicSource == null)
        {
            GameObject musicGO = new("MusicSource");
            musicGO.transform.SetParent(transform);
            musicSource = musicGO.AddComponent<AudioSource>();
            musicSource.loop = true;
            musicSource.playOnAwake = false;
        }
        _currentMusicSource = musicSource;

        for (int i = 0; i < soundSourcePoolSize; i++)
        {
            GameObject sourceGo = new GameObject($"SoundSource_{i}");
            sourceGo.transform.SetParent(transform);
            AudioSource source = sourceGo.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f; // 2D sound
            source.minDistance = minDistance3D;
            source.maxDistance = maxDistance3D;
            _availableSources.Enqueue(source);
        }
    }

    private void Update()
    {
        for (int i = _activeSources.Count - 1; i >= 0; i--)
        {
            if (!_activeSources[i].isPlaying)
            {
                AudioSource source = _activeSources[i];
                _activeSources.RemoveAt(i);
                source.Stop();
                source.clip = null;
                _availableSources.Enqueue(source);
            }
        }
    }

    public void PlaySound(AudioClip clip, AudioCategory category, Vector3? position = null, float? volume = null)
    {
        if (clip == null)
        {
            Debug.LogWarning("[AudioService] Attemped to play null audioClip");
            return;
        }

        AudioSource source = GetAvailableSource();

        if (source == null)
        {
            Debug.LogWarning("[AudioService] No available audio sources");
            return;
        }

        source.clip = clip;
        source.loop = false;

        if (position.HasValue)
        {
            source.spatialBlend = 1f;
            source.transform.position = position.Value;
        }
        else
        {
            // 2D sounds
            source.spatialBlend = 0f;
        }

        // Calculate volume
        float categoryVol = GetCategoryVolume(category);
        float finalVolume = (volume ?? categoryVol) * GetCategoryVolume(AudioCategory.Master);
        source.volume = finalVolume;

        source.Play();
        _activeSources.Add(source);
    }

    public void PlayMusic(AudioClip clip, float fadeIn = 1)
    {
        if (clip == null)
        {
            Debug.LogWarning("[AudioService] Attempted to play null music audioClip");
            return;
        }

        if (_musicFadeCoroutine != null)
        {
            StopCoroutine(_musicFadeCoroutine);
        }

        _musicFadeCoroutine = StartCoroutine(FadeInMusicCoroutine(clip, fadeIn));
    }

    public float GetCategoryVolume(AudioCategory category)
    {
        return _categoryVolumes.TryGetValue(category, out float volume) ? volume : 1f;
    }

    public void SetCategoryVolume(AudioCategory category, float volume)
    {
        volume = Mathf.Clamp01(volume);
        _categoryVolumes[category] = volume;

        if (category == AudioCategory.Master || category == AudioCategory.Music)
        {
            if (_currentMusicSource != null && _currentMusicSource.isPlaying)
            {
                _currentMusicSource.volume =
                    GetCategoryVolume(AudioCategory.Music) * GetCategoryVolume(AudioCategory.Master);
            }
        }

        // Update active sound sources
        foreach (var source in _activeSources)
        {
            if (source.isPlaying && source.clip != null)
            {
                source.volume = source.volume;
            }
        }
    }

    public float GetMasterVolume()
    {
        return GetCategoryVolume(AudioCategory.Master);
    }

    public void SetMasterVolume(float volume)
    {
        SetCategoryVolume(AudioCategory.Master, volume);
    }

    public void StopMusic(float fadeOut = 1)
    {
        if (_musicFadeCoroutine != null)
        {
            StopCoroutine(_musicFadeCoroutine);
        }

        _musicFadeCoroutine = StartCoroutine(FadeOutMusicCoroutine(fadeOut));
    }

    private AudioSource GetAvailableSource()
    {
        if (_availableSources.Count > 0)
        {
            return _availableSources.Dequeue();
        }

        GameObject sourceGO = new("SoundSource_Dynamic");
        sourceGO.transform.SetParent(transform);
        AudioSource source = sourceGO.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.minDistance = minDistance3D;
        source.maxDistance = maxDistance3D;
        return source;
    }

    private IEnumerator FadeInMusicCoroutine(AudioClip newClip, float fadeIn)
    {
        if (_currentMusicSource.isPlaying)
        {
            float startVolume = _currentMusicSource.volume;
            float elapsed = 0f;
            float fadeoutTime = 0.5f;

            while (elapsed < fadeoutTime)
            {
                elapsed += Time.deltaTime;
                _currentMusicSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / fadeoutTime);
                yield return null;
            }
            _currentMusicSource.Stop();
        }

        // Play new music
        _currentMusicSource.clip = newClip;
        _currentMusicSource.volume = 0f;
        _currentMusicSource.Play();

        // Fade in
        float targetVolume = GetCategoryVolume(AudioCategory.Music) * GetCategoryVolume(AudioCategory.Master);
        float elapsedIn = 0f;

        while (elapsedIn < fadeIn)
        {
            elapsedIn += Time.deltaTime;
            _currentMusicSource.volume = Mathf.Lerp(0f, targetVolume, elapsedIn / fadeIn);
            yield return null;
        }

        _currentMusicSource.volume = targetVolume;
        _musicFadeCoroutine = null;
    }

    private IEnumerator FadeOutMusicCoroutine(float fadeOut)
    {
        if (!_currentMusicSource.isPlaying)
        {
            yield break;
        }

        float startVolume = _currentMusicSource.volume;
        float elapsed = 0f;

        while (elapsed < fadeOut)
        {
            elapsed += Time.deltaTime;
            _currentMusicSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / fadeOut);
            yield return null;
        }

        _currentMusicSource.Stop();
        _musicFadeCoroutine = null;
    }
}
