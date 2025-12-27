using TMPro;
using UnityEngine;

/// <summary>
/// UI for spectators in the Character Selection scene.
/// Shows a message indicating they are spectating and waiting for the game to start.
/// </summary>
public class SpectatorCharacterSelectionUI : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField]
    private GameObject _spectatorPanel;

    [SerializeField]
    private TextMeshProUGUI _statusText;

    private void Start()
    {
        // Check if local client is a spectator
        bool isSpectator = ConnectionSettings.IsSpectator;

        if (isSpectator)
        {
            // Show spectator panel
            if (_spectatorPanel != null)
            {
                _spectatorPanel.SetActive(true);
            }

            if (_statusText != null)
            {
                _statusText.text = "You are spectating.\nWaiting for players to start the game...";
            }

            Debug.Log("[SpectatorCharacterSelectionUI] Spectator mode - showing spectator panel");
        }
        else
        {
            // Hide spectator panel for regular players
            if (_spectatorPanel != null)
            {
                _spectatorPanel.SetActive(false);
            }
        }
    }
}
