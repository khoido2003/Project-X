using System.Collections;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class MenuManager : MonoBehaviour
{
    [SerializeField]
    private CharacterDefinitionSO[] m_characterDatas;

    [SerializeField]
    private AudioClip m_confirmClip;

    private bool m_pressAnyKeyActive = true;

    [SerializeField]
    private SceneName nextScene = SceneName.CharacterSelection;

    [SerializeField]
    private TextMeshProUGUI m_pressAnyKeyText;

    [SerializeField]
    private Button m_hostBtn;

    [SerializeField]
    private Button m_joinBtn;

    [SerializeField]
    private Button m_quickGameBtn;

    [SerializeField]
    private UISoundConfig uiSoundConfig;

    private void Awake() { }

    private IEnumerator Start()
    {
        ClearAllCharactersData();

        m_hostBtn.onClick.AddListener(() =>
        {
            OnClickHost();
        });

        m_joinBtn.onClick.AddListener(() =>
        {
            OnClickJoin();
        });

        m_quickGameBtn.onClick.AddListener(() =>
        {
            OnClickQuit();
        });

        m_hostBtn.gameObject.SetActive(false);
        m_joinBtn.gameObject.SetActive(false);
        m_quickGameBtn.gameObject.SetActive(false);

        yield return new WaitUntil(() => NetworkManager.Singleton.SceneManager != null);
        LoadingSceneManager.Instance.Init();
    }

    private void Update()
    {
        if (m_pressAnyKeyActive)
        {
            if (Input.anyKey)
            {
                TriggerMainMenuTransition();
                m_pressAnyKeyActive = false;
            }
        }
    }

    public void OnClickHost()
    {
        PlayButtonClickSound();
        NetworkManager.Singleton.StartHost();
        LoadingSceneManager.Instance.LoadScene(nextScene);

    }

    public void OnClickJoin()
    {
        PlayButtonClickSound();
        StartCoroutine(Join());
    }

    public void OnClickQuit()
    {
        PlayButtonClickSound();
        Application.Quit();
    }

    private void PlayButtonClickSound()
    {
        if (uiSoundConfig != null && uiSoundConfig.buttonClick != null && AudioService.Instance != null)
        {
            AudioHelper.PlaySound(uiSoundConfig.buttonClick, AudioCategory.UI, uiSoundConfig.uiSoundVolume);
        }
        else if (m_confirmClip != null && AudioService.Instance != null)
        {
            // Fallback to legacy clip
            AudioHelper.PlaySound(m_confirmClip, AudioCategory.UI);
        }
    }

    private void ClearAllCharactersData()
    {
        foreach (CharacterDefinitionSO data in m_characterDatas)
        {
            data.EmptyData();
        }
    }

    private void TriggerMainMenuTransition()
    {
        m_pressAnyKeyText.gameObject.SetActive(false);

        m_hostBtn.gameObject.SetActive(true);
        m_joinBtn.gameObject.SetActive(true);
        m_quickGameBtn.gameObject.SetActive(true);
    }

    private IEnumerator Join()
    {
        LoadingFadeEffect.Instance.FadeAll();

        yield return new WaitUntil(() => LoadingFadeEffect.s_canLoad);

        // Subscribe to connection/disconnection events
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;

        NetworkManager.Singleton.StartClient();

        // Wait a bit to see if connection succeeds
        yield return new WaitForSeconds(5f);

        // If still not connected, show error and return to menu
        if (!NetworkManager.Singleton.IsConnectedClient)
        {
            Debug.LogWarning("Failed to connect to host. Returning to menu.");
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.Shutdown();
            LoadingFadeEffect.Instance.FadeOut();
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        // Connection successful, unsubscribe from events
        NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
    }

    private void OnClientDisconnected(ulong clientId)
    {
        // Only handle if we're the disconnected client (not server disconnecting us)
        if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsServer)
        {
            Debug.LogWarning("Disconnected from host. Returning to menu.");
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;

            // Shutdown and return to menu
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.Shutdown();
            }

            LoadingFadeEffect.Instance.FadeOut();
            LoadingSceneManager.Instance.LoadScene(SceneName.Menu, false);
        }
    }
}
