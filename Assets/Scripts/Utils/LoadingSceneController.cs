using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LoadingSceneController : MonoBehaviour
{
    [SerializeField]
    private TextMeshProUGUI loadingText;

    private static string nextScene;

    public static void LoadScene(string sceneName)
    {
        nextScene = sceneName;
        SceneManager.LoadScene("LoadingScene");
    }

    private void Start()
    {
        StartCoroutine(LoadAsync());
    }

    private IEnumerator LoadAsync()
    {
        // Small delay for fade-in or logo animation
        yield return new WaitForSeconds(0.3f);

        AsyncOperation op = SceneManager.LoadSceneAsync(nextScene);
        op.allowSceneActivation = false;

        while (!op.isDone)
        {
            float progress = Mathf.Clamp01(op.progress / 0.9f);
            loadingText.text = $"Loading... {Mathf.RoundToInt(progress * 100)}%";

            if (progress >= 1f)
            {
                yield return new WaitForSeconds(0.5f);
                op.allowSceneActivation = true;
            }

            yield return null;
        }
    }
}
