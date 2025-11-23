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

    private void Awake()
    {
        Debug.Log("MenuManager Awake");
    }

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
        NetworkManager.Singleton.StartHost();
        LoadingSceneManager.Instance.LoadScene(nextScene);
        Debug.Log("Host clicked");
    }

    public void OnClickJoin()
    {
        StartCoroutine(Join());
    }

    public void OnClickQuit()
    {
        Application.Quit();
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

        NetworkManager.Singleton.StartClient();
    }
}
