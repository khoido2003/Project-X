using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerRespawnUI : MonoBehaviour
{
    [Header("UI Reference")]
    [SerializeField]
    private GameObject respawnPanel;

    [SerializeField]
    private TextMeshProUGUI respawnTimerText;

    [SerializeField]
    private Slider respawnFillBar;

    private float _respawnTimeRemaining;
    private float _totalRespawnTime;
    private bool _isRespawning;

    private void Awake()
    {
        if (respawnPanel != null)
        {
            respawnPanel.SetActive(false);
        }
    }

    private void Update()
    {
        if (_isRespawning)
        {
            _respawnTimeRemaining -= Time.deltaTime;

            if (respawnTimerText != null)
            {
                respawnTimerText.text = $"Respawning in: {Mathf.CeilToInt(_respawnTimeRemaining)}s";
            }

            if (respawnFillBar != null)
            {
                respawnFillBar.value = _respawnTimeRemaining / _totalRespawnTime;
            }

            if (_respawnTimeRemaining <= 0f)
            {
                HideRespawnTimer();
            }
        }
    }

    public void ShowRespawnTimer(float duration)
    {
        _isRespawning = true;
        _respawnTimeRemaining = duration;
        _totalRespawnTime = duration;

        if (respawnPanel != null)
        {
            respawnPanel.SetActive(true);
        }
    }

    public void HideRespawnTimer()
    {
        _isRespawning = false;

        if (respawnPanel != null)
        {
            respawnPanel.SetActive(false);
        }
    }
}
