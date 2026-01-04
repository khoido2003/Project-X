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

    [Header("Audio")]
    [SerializeField]
    private AudioClip victoryMusic;

    [SerializeField]
    private AudioClip defeatMusic;

    [SerializeField]
    [Range(0f, 2f)]
    private float musicFadeIn = 1f;

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

        // Play victory/defeat music based on local player placement
        PlayResultMusic(results);

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

    private void PlayResultMusic(PlayerResult[] results)
    {
        if (AudioService.Instance == null)
        {
            return;
        }

        bool isVictory = false;
        if (results != null && results.Length > 0)
        {
            ulong targetClientId = NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClientId : 0;

            // SPECIAL CASE: If we are a spectator, check result for the player we are following
            if (SpectatorSpawner.Instance != null && SpectatorSpawner.Instance.IsLocalSpectator)
            {
                var spectatorController = FindFirstObjectByType<SpectatorController>();
                if (spectatorController != null && !spectatorController.FollowedPlayerEntity.Equals(default))
                {
                    if (WorldRunner.Instance != null && WorldRunner.Instance.World != null)
                    {
                        if (WorldRunner.Instance.World.Components.TryGet(spectatorController.FollowedPlayerEntity, out NetworkOwnerComponent owner))
                        {
                            targetClientId = owner.OwnerClientId;
                            Debug.Log($"[GameResultsUI] Spectating entity {spectatorController.FollowedPlayerEntity.Id}, using result for ClientId {targetClientId}");
                        }
                    }
                }
            }

            // Consider victory if target player is in the top 1
            for (int i = 0; i < Mathf.Min(1, results.Length); i++)
            {
                if (results[i].ClientId == targetClientId)
                {
                    isVictory = true;
                    break;
                }
            }
        }

        AudioClip clip = isVictory ? victoryMusic : defeatMusic;
        if (clip != null)
        {
            AudioHelper.PlayMusic(clip, musicFadeIn);
        }
    }
}
