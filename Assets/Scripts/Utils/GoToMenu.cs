using System.Collections;
using UnityEngine;

public class GoToMenu : MonoBehaviour
{
    private IEnumerator Start()
    {
        yield return new WaitUntil(() => LoadingSceneManager.Instance != null);

        LoadingSceneManager.Instance.LoadScene(SceneName.Menu, false);
    }
}
