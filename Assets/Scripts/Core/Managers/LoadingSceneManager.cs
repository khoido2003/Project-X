using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum SceneName : byte
{
    Bootstrap,
    Menu,
    CharacterSelection,
    Controls,

    Map_1,
    Map_2,
    Map_3,

    Victory,
    Defeat,
    Loading,
}

public class LoadingSceneManager : SingletonPersistent<LoadingSceneManager>
{
    public SceneName SceneActive => m_sceneActive;

    [Header("Audio Configuration")]
    [SerializeField]
    private SceneAudioConfig sceneAudioConfig;

    private SceneName m_sceneActive;

    public static event Action OnLoadingStarted;
    public static event Action OnLoadingFinished;

    public void Init()
    {
        NetworkManager.Singleton.SceneManager.OnLoadComplete -= OnLoadComplete;
        NetworkManager.Singleton.SceneManager.OnLoadComplete += OnLoadComplete;
    }

    public void LoadScene(SceneName sceneToLoad, bool isNetworkSessionActive = true)
    {
        StartCoroutine(Loading(sceneToLoad, isNetworkSessionActive));
    }

    private IEnumerator Loading(SceneName sceneToLoad, bool isNetworkSessionActive)
    {
        LoadingFadeEffect.Instance.FadeIn();

        yield return new WaitUntil(() => LoadingFadeEffect.s_canLoad);

        // Load the loadinng screen first
        if (isNetworkSessionActive)
        {
            if (NetworkManager.Singleton.IsServer)
            {
                LoadSceneNetwork(SceneName.Loading);
            }
        }
        else
        {
            LoadSceneLocal(SceneName.Loading);
        }

        yield return new WaitUntil(() => m_sceneActive == SceneName.Loading);

        // Load the real scene
        if (isNetworkSessionActive)
        {
            if (NetworkManager.Singleton.IsServer)
            {
                LoadSceneNetwork(sceneToLoad);
            }
        }
        else
        {
            yield return StartCoroutine(LoadSceneLocalAsync(sceneToLoad));
        }

        LoadingFadeEffect.Instance.FadeOut();
    }

    private void LoadSceneNetwork(SceneName sceneToLoad)
    {
        NetworkManager.Singleton.SceneManager.LoadScene(sceneToLoad.ToString(), LoadSceneMode.Single);
        OnLoadingStarted?.Invoke();
    }

    private void LoadSceneLocal(SceneName sceneToLoad)
    {
        OnLoadingStarted?.Invoke();
        SceneManager.LoadScene(sceneToLoad.ToString());

        m_sceneActive = sceneToLoad;
        PlaySceneMusic(sceneToLoad);
    }

    private IEnumerator LoadSceneLocalAsync(SceneName sceneToLoad)
    {
        OnLoadingStarted?.Invoke();

        m_sceneActive = sceneToLoad;

        AsyncOperation asyncOp = SceneManager.LoadSceneAsync(sceneToLoad.ToString());
        asyncOp.allowSceneActivation = true;

        while (!asyncOp.isDone)
        {
            float progress = Mathf.Clamp01(asyncOp.progress / 0.9f);

            if (progress >= 1f)
            {
                asyncOp.allowSceneActivation = true;
            }

            yield return null;
        }

        // Play music after scene is loaded
        PlaySceneMusic(sceneToLoad);
    }

    private void OnLoadComplete(ulong clientId, string sceneName, LoadSceneMode loadSceneMode)
    {
        Enum.TryParse(sceneName, out m_sceneActive);

        // Play scene music for BOTH client and server when scene loads
        // NOTE: LoadingSceneManager is NOT a NetworkBehaviour, so ClientRpc doesn't work.
        // Each client plays music when their own scene load completes.
        PlaySceneMusic(m_sceneActive);

        if (!NetworkManager.Singleton.IsServer)
        {
            return;
        }

        if (!ClientConnection.Instance.CanClientConnect(clientId))
        {
            return;
        }

        // Use coroutine to handle client initialization with spectator check delay
        StartCoroutine(HandleClientSceneLoad(clientId, sceneName));

        OnLoadingFinished?.Invoke();
    }
    
    /// <summary>
    /// Handle client scene load with a small delay to allow spectator RPC to arrive.
    /// This fixes the race condition where spectator registration RPC arrives after OnLoadComplete.
    /// </summary>
    private IEnumerator HandleClientSceneLoad(ulong clientId, string sceneName)
    {
        // Parse the scene name to get the enum
        if (!Enum.TryParse(sceneName, out SceneName loadedScene))
        {
            Debug.LogWarning($"[LoadingSceneManager] Unknown scene: {sceneName}");
            yield break;
        }
        
        // Skip Loading scene - we don't process players during loading
        if (loadedScene == SceneName.Loading)
        {
            yield break;
        }
        
        // Wait a small amount for spectator registration RPC to arrive
        // This delay is necessary because RPCs are processed asynchronously
        yield return new WaitForSeconds(0.3f);
        
        // Now check if this client is a spectator
        bool isSpectator = SpectatorNetworkHandler.Instance != null && 
                           SpectatorNetworkHandler.Instance.IsSpectator(clientId);
        
        Debug.Log($"[LoadingSceneManager] Client {clientId} loaded {sceneName}. IsSpectator: {isSpectator}");

        switch (loadedScene)
        {
            case SceneName.CharacterSelection:
                // Spectators skip character selection entirely
                if (isSpectator)
                {
                    Debug.Log($"[LoadingSceneManager] Spectator {clientId} skipping character selection - no player object spawned");
                }
                else if (CharacterSelectionManager.Instance != null)
                {
                    CharacterSelectionManager.Instance.ServerSceneInit(clientId);
                }
                else
                {
                    StartCoroutine(WaitForCharacterSelectionManager(clientId));
                }
                break;

            case SceneName.Map_1:
            case SceneName.Map_2:
            case SceneName.Map_3:
                if (isSpectator)
                {
                    Debug.Log($"[LoadingSceneManager] Spectator {clientId} successfully loaded into gameplay scene: {sceneName}");
                    // Spectator is good - SpectatorSpawner will handle camera setup
                }
                else
                {
                    // Check if this is a valid player (has character selected)
                    bool hasCharacter = HasCharacterSelected(clientId);
                    
                    if (hasCharacter)
                    {
                        Debug.Log($"[LoadingSceneManager] Player {clientId} loaded into gameplay scene: {sceneName}");
                    }
                    else
                    {
                        // This is an invalid late-joiner - not a spectator and no character
                        Debug.LogWarning($"[LoadingSceneManager] Rejecting client {clientId} - not a spectator and no character selected. Disconnecting...");
                        DisconnectClient(clientId);
                    }
                }
                break;

            case SceneName.Victory:
            case SceneName.Defeat:
                break;
        }
    }
    
    /// <summary>
    /// Check if a client has selected a character.
    /// </summary>
    private bool HasCharacterSelected(ulong clientId)
    {
        // Check CharacterSelectionManager for character data
        if (CharacterSelectionManager.Instance != null)
        {
            return CharacterSelectionManager.Instance.HasCharacterForClient(clientId);
        }
        return false;
    }
    
    /// <summary>
    /// Disconnect a client that shouldn't be in the game.
    /// </summary>
    private void DisconnectClient(ulong clientId)
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            NetworkManager.Singleton.DisconnectClient(clientId);
        }
    }

    private void PlaySceneMusic(SceneName sceneName)
    {
        if (sceneAudioConfig == null || AudioService.Instance == null)
        {
            return;
        }

        AudioClip music = sceneAudioConfig.GetMusicForScene(sceneName);
        if (music != null)
        {
            AudioHelper.PlayMusic(music, sceneAudioConfig.musicFadeInTime);
        }
    }

    private IEnumerator WaitForCharacterSelectionManager(ulong clientId)
    {
        yield return new WaitUntil(() => CharacterSelectionManager.Instance != null);
        
        // Wait for spectator status to be known (same delay as HandleClientSceneLoad)
        yield return new WaitForSeconds(0.3f);
        
        // Check if spectator - don't spawn player for spectators
        bool isSpectator = SpectatorNetworkHandler.Instance != null && 
                           SpectatorNetworkHandler.Instance.IsSpectator(clientId);
        
        if (!isSpectator)
        {
            CharacterSelectionManager.Instance.ServerSceneInit(clientId);
        }
        else
        {
            Debug.Log($"[LoadingSceneManager] Spectator {clientId} skipping ServerSceneInit");
        }
    }
}
