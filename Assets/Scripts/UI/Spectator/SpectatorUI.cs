using TMPro;
using UnityEngine;

/// <summary>
/// UI overlay for spectators showing current mode and controls.
/// Attach to a Canvas that is a child of SpectatorCameraRig.
/// </summary>
public class SpectatorUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI _modeText;
    [SerializeField] private TextMeshProUGUI _controlsText;
    [SerializeField] private TextMeshProUGUI _playerNameText;
    [SerializeField] private GameObject _playerFollowPanel;
    
    [Header("Settings")]
    [SerializeField] private Color _overviewColor = new Color(0.3f, 0.7f, 1f);
    [SerializeField] private Color _followColor = new Color(1f, 0.7f, 0.3f);
    
    private SpectatorController _controller;

    private void Start()
    {
        _controller = GetComponentInParent<SpectatorController>();
        if (_controller == null)
        {
            _controller = FindFirstObjectByType<SpectatorController>();
        }
        
        if (_controller != null)
        {
            _controller.OnModeChanged += OnModeChanged;
            _controller.OnFollowTargetChanged += OnFollowTargetChanged;
            
            // Initialize with current state
            OnModeChanged(_controller.CurrentMode);
        }
        
        UpdateControlsText(SpectatorController.SpectatorMode.Overview);
    }

    private void OnDestroy()
    {
        if (_controller != null)
        {
            _controller.OnModeChanged -= OnModeChanged;
            _controller.OnFollowTargetChanged -= OnFollowTargetChanged;
        }
    }

    private void OnModeChanged(SpectatorController.SpectatorMode mode)
    {
        if (_modeText != null)
        {
            switch (mode)
            {
                case SpectatorController.SpectatorMode.Overview:
                    _modeText.text = "SPECTATOR - Overview Mode";
                    _modeText.color = _overviewColor;
                    break;
                case SpectatorController.SpectatorMode.PlayerFollow:
                    _modeText.text = "SPECTATOR - Following Player";
                    _modeText.color = _followColor;
                    break;
            }
        }
        
        UpdateControlsText(mode);
        
        // Show/hide player follow panel
        if (_playerFollowPanel != null)
        {
            _playerFollowPanel.SetActive(mode == SpectatorController.SpectatorMode.PlayerFollow);
        }
    }

    private void OnFollowTargetChanged(string playerName)
    {
        if (_playerNameText != null)
        {
            _playerNameText.text = string.IsNullOrEmpty(playerName) 
                ? "No players found" 
                : $"Following: {playerName}";
        }
    }

    private void UpdateControlsText(SpectatorController.SpectatorMode mode)
    {
        if (_controlsText == null) return;
        
        switch (mode)
        {
            case SpectatorController.SpectatorMode.Overview:
                _controlsText.text = 
                    "<b>Controls:</b>\n" +
                    "WASD - Move\n" +
                    "Right Click + Mouse - Look\n" +
                    "Shift - Speed Up\n" +
                    "E/Space - Up | Q/Ctrl - Down\n" +
                    "<color=#FFD700>Tab - Switch to Follow Mode</color>";
                break;
            case SpectatorController.SpectatorMode.PlayerFollow:
                _controlsText.text = 
                    "<b>Controls:</b>\n" +
                    "← → or A/D - Switch Player\n" +
                    "<color=#FFD700>Tab - Switch to Overview Mode</color>";
                break;
        }
    }
}
