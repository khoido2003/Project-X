using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class GameResultsUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField]
    private GameObject resultsPanel;

    [SerializeField]
    private Transform resultsContainer;

    [SerializeField]
    private GameObject resultEntryPrefab;

    [SerializeField]
    private TextMeshProUGUI winnerText;

    [SerializeField]
    private Button returnToLobbyButton;

    private void Awake()
    {
        if (resultsPanel != null)
        {
            resultsPanel.SetActive(false);
        }

        if (returnToLobbyButton != null)
        {
            returnToLobbyButton.onClick.AddListener(OnReturnToLobby);
        }
    }

    public void DisplayResults(PlayerResult[] results)
    {
        if (resultsPanel != null)
        {
            resultsPanel.SetActive(true);
        }

        // Clear previous results
        foreach (Transform child in resultsContainer)
        {
            Destroy(child.gameObject);
        }

        // Display winner
        if (results.Length > 0 && winnerText != null)
        {
            winnerText.text = $"WINNER: {results[0].PlayerName}";
        }

        // Display all results
        for (int i = 0; i < results.Length; i++)
        {
            var result = results[i];
            GameObject entry = Instantiate(resultEntryPrefab, resultsContainer);

            // Setup entry (assuming it has these text components)
            var texts = entry.GetComponentsInChildren<TextMeshProUGUI>();
            if (texts.Length >= 5)
            {
                texts[0].text = $"{i + 1}"; // Rank
                texts[1].text = result.PlayerName;
                texts[2].text = result.TotalScore.ToString();
                texts[3].text = $"Enemies: {result.EnemyKills}";
                texts[4].text = $"Players: {result.PlayerKills}";
            }
        }

        Debug.Log($"[GameResultsUI] Displayed results for {results.Length} players");
    }

    private void OnReturnToLobby()
    {
        // Disconnect and return to main menu
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.Shutdown();
        }

        // Load main menu scene
        LoadingSceneManager.Instance.LoadScene(SceneName.Menu, false);
    }

    public void HideResults()
    {
        if (resultsPanel != null)
        {
            resultsPanel.SetActive(false);
        }
    }
}
