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

    [Header("Volume Settings")]
    [SerializeField]
    [Range(0f, 1f)]
    private float masterVolume = 1f;

    [SerializeField]
    [Range(0f, 1f)]
    private float musicVolume = 0.3f;

    [SerializeField]
    [Range(0f, 1f)]
    private float uiVolume = 1f;

    [SerializeField]
    [Range(0f, 1f)]
    private float playerVolume = 1f;

    [SerializeField]
    [Range(0f, 1f)]
    private float enemyVolume = 1f;

    [SerializeField]
    [Range(0f, 1f)]
    private float environmentVolume = 1f;

    [Header("Sound Effect Boost")]
    [SerializeField]
    [Range(0.5f, 5f)]
    [Tooltip(
        "Multiplier to boost sound effect volumes relative to music. Higher values make SFX louder. Default 3.0 for quiet audio files."
    )]
    private float soundEffectVolumeMultiplier = 4f;

    [Header("3D Audio Settings")]
    [SerializeField]
    private float minDistance3D = 1f;

    [SerializeField]
    private float maxDistance3D = 50f;

    private Queue<AudioSource> _availableSources = new();
    private List<AudioSource> _activeSources = new();
    private Dictionary<AudioCategory, float> _categoryVolumes = new();
    private AudioSource _currentMusicSource;
    private Coroutine _musicFadeCoroutine;
    private Dictionary<string, AudioSource> _activeFootstepSources = new();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Initialize category volumes
        _categoryVolumes[AudioCategory.Master] = masterVolume;
        _categoryVolumes[AudioCategory.Music] = musicVolume;
        _categoryVolumes[AudioCategory.UI] = uiVolume;
        _categoryVolumes[AudioCategory.Player] = playerVolume;
        _categoryVolumes[AudioCategory.Enemy] = enemyVolume;
        _categoryVolumes[AudioCategory.Environment] = environmentVolume;

        // Setup music source
        if (musicSource == null)
        {
            GameObject musicGO = new("MusicSource");
            musicGO.transform.SetParent(transform);
            musicSource = musicGO.AddComponent<AudioSource>();
            musicSource.loop = true;
            musicSource.playOnAwake = false;
        }
        _currentMusicSource = musicSource;
        _currentMusicSource.volume = GetCategoryVolume(AudioCategory.Music) * GetCategoryVolume(AudioCategory.Master);

        // Create sound source pool
        for (int i = 0; i < soundSourcePoolSize; i++)
        {
            GameObject sourceGo = new GameObject($"SoundSource_{i}");
            sourceGo.transform.SetParent(transform);
            AudioSource source = sourceGo.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
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
        PlaySoundForEntity(clip, category, position, volume, default(EntityId));
    }

    public AudioSource PlaySoundForEntity(
        AudioClip clip,
        AudioCategory category,
        Vector3? position = null,
        float? volume = null,
        EntityId entity = default
    )
    {
        return PlaySoundForEntity(clip, category, position, volume, entity.Id.ToString());
    }

    private AudioSource PlaySoundForEntity(
        AudioClip clip,
        AudioCategory category,
        Vector3? position,
        float? volume,
        string entityKey
    )
    {
        if (clip == null)
        {
            Debug.LogWarning("[AudioService] Attempted to play null audioClip");
            return null;
        }

        AudioSource source = GetAvailableSource();
        if (source == null)
        {
            Debug.LogWarning("[AudioService] No available audio sources");
            return null;
        }

        source.clip = clip;
        source.loop = false;

        // Setup 3D audio if position provided
        if (position.HasValue)
        {
            source.spatialBlend = 1f;
            source.transform.position = position.Value;
        }
        else
        {
            source.spatialBlend = 0f;
        }

        // Calculate final volume: clipVolume * categoryVolume * masterVolume * soundEffectMultiplier
        // Apply sound effect multiplier for all non-music sounds to make them louder relative to music
        float clipVolume = volume ?? 1f;
        float categoryVol = GetCategoryVolume(category);
        float masterVol = GetCategoryVolume(AudioCategory.Master);
        float volumeMultiplier = (category == AudioCategory.Music) ? 1f : soundEffectVolumeMultiplier;
        float finalVolume = clipVolume * categoryVol * masterVol * volumeMultiplier;
        source.volume = Mathf.Clamp01(finalVolume);

        source.Play();
        _activeSources.Add(source);

        return source;
    }

    public void StopFootstepForEntity(EntityId entity)
    {
        string entityKey = entity.Id.ToString();
        if (_activeFootstepSources.TryGetValue(entityKey, out AudioSource source))
        {
            if (source != null && source.isPlaying)
            {
                source.Stop();
                source.clip = null;
                if (_activeSources.Contains(source))
                {
                    _activeSources.Remove(source);
                }
                _availableSources.Enqueue(source);
            }
            _activeFootstepSources.Remove(entityKey);
        }
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

        _currentMusicSource.clip = newClip;
        _currentMusicSource.volume = 0f;
        _currentMusicSource.Play();

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
