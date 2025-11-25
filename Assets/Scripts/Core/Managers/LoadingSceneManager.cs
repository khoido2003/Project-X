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
    Gameplay,
    Victory,
    Defeat,
    Loading,
}

public class LoadingSceneManager : SingletonPersistent<LoadingSceneManager>
{
    public SceneName SceneActive => m_sceneActive;

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
        switch (sceneToLoad)
        {
            case SceneName.Loading:
                break;

            case SceneName.Menu:

                // TODO:  Load sound
                break;
        }
    }

    private IEnumerator LoadSceneLocalAsync(SceneName sceneToLoad)
    {
        OnLoadingStarted?.Invoke();

        m_sceneActive = sceneToLoad;

        AsyncOperation asyncOp = SceneManager.LoadSceneAsync(sceneToLoad.ToString());
        asyncOp.allowSceneActivation = true;

        switch (sceneToLoad)
        {
            case SceneName.Loading:
                break;

            case SceneName.Menu:

                // TODO:  Load sound
                break;
        }

        while (!asyncOp.isDone)
        {
            float progress = Mathf.Clamp01(asyncOp.progress / 0.9f);

            if (progress >= 1f)
            {
                asyncOp.allowSceneActivation = true;
            }

            yield return null;
        }
    }

    private void OnLoadComplete(ulong clientId, string sceneName, LoadSceneMode loadSceneMode)
    {
        if (!NetworkManager.Singleton.IsServer)
        {
            return;
        }

        Enum.TryParse(sceneName, out m_sceneActive);

        if (!ClientConnection.Instance.CanClientConnect(clientId))
        {
            return;
        }

        switch (m_sceneActive)
        {
            case SceneName.CharacterSelection:
                if (CharacterSelectionManager.Instance != null)
                {
                    CharacterSelectionManager.Instance.ServerSceneInit(clientId);
                }
                else
                {
                    StartCoroutine(WaitForCharacterSelectionManager(clientId));
                }
                break;

            case SceneName.Gameplay:
                break;

            case SceneName.Victory:
            case SceneName.Defeat:
                break;
        }

        OnLoadingFinished?.Invoke();
    }

    private IEnumerator WaitForCharacterSelectionManager(ulong clientId)
    {
        yield return new WaitUntil(() => CharacterSelectionManager.Instance != null);
        CharacterSelectionManager.Instance.ServerSceneInit(clientId);
    }
}
