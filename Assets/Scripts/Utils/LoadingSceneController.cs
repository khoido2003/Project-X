using TMPro;
using UnityEngine;

public class LoadingSceneController : MonoBehaviour
{
    [SerializeField]
    private TextMeshProUGUI loadingText;

    private bool isLoading = false;
    private float fakeProgress = 0f;

    private void OnEnable()
    {
        LoadingSceneManager.OnLoadingStarted += BeginLoading;
        LoadingSceneManager.OnLoadingFinished += EndLoading;
    }

    private void OnDisable()
    {
        LoadingSceneManager.OnLoadingStarted -= BeginLoading;
        LoadingSceneManager.OnLoadingFinished -= EndLoading;
    }

    void BeginLoading()
    {
        isLoading = true;
        fakeProgress = 0f;
    }

    void EndLoading()
    {
        isLoading = false;
        fakeProgress = 1f;
        loadingText.text = "Loading... 100%";
        loadingText.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (!isLoading)
            return;

        // Fake progress curve
        fakeProgress = Mathf.MoveTowards(fakeProgress, 0.95f, Time.deltaTime * 0.3f);

        loadingText.text = $"Loading... {Mathf.RoundToInt(fakeProgress * 100)}%";
    }
}
