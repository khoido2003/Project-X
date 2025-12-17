using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameHUD : MonoBehaviour
{
    [Header("Phase Display")]
    [SerializeField]
    private TextMeshProUGUI phaseText;

    [SerializeField]
    private TextMeshProUGUI roundText;

    [SerializeField]
    private TextMeshProUGUI timerText;

    [SerializeField]
    private Slider timerFillBar;

    [Header("Local Player Stats")]
    [SerializeField]
    private TextMeshProUGUI scoreText;

    [SerializeField]
    private TextMeshProUGUI killsText;

    [SerializeField]
    private Slider healthBar;

    [SerializeField]
    private TextMeshProUGUI healthText;

    [Header("All Players Display")]
    [SerializeField]
    private Transform allPlayersContainer;

    [SerializeField]
    private GameObject playerStatEntryPrefab;

    private World _world;
    private EntityId _localPlayerEntity;
    private Dictionary<ulong, GameObject> _playerEntries = new();

    private bool _isInitialized = false;

    private void Start()
    {
        StartCoroutine(InitializeWhenReady());
    }

    private IEnumerator InitializeWhenReady()
    {
        // Wait for WorldRunner and World to be ready
        while (WorldRunner.Instance == null || WorldRunner.Instance.World == null)
        {
            yield return null;
        }

        _world = WorldRunner.Instance.World;

        if (NetworkGameStateManager.Instance != null)
        {
            NetworkGameStateManager.Instance.OnPhaseChanged += OnPhaseChanged;
        }

        _world.Events.Subscribe<HealthChangedEvent>(OnHealthChanged);

        FindLocalPlayerEntity();
        _isInitialized = true;

        Debug.Log("[GameHUD] Initialized successfully");
    }

    private void OnDestroy()
    {
        if (NetworkGameStateManager.Instance != null)
        {
            NetworkGameStateManager.Instance.OnPhaseChanged -= OnPhaseChanged;
        }

        if (_world != null)
        {
            _world.Events.Unsubscribe<HealthChangedEvent>(OnHealthChanged);
        }
    }

    private void Update()
    {
        if (!_isInitialized || _world == null)
        {
            return;
        }

        if (NetworkGameStateManager.Instance != null)
        {
            float timeRemaining = NetworkGameStateManager.Instance.PhaseTimeRemaining;

            if (timerText != null)
            {
                timerText.text = $"Time: {Mathf.CeilToInt(timeRemaining)}s";
            }

            if (timerFillBar != null)
            {
                float maxTime = GetMaxTimeForCurrentPhase();
                timerFillBar.value = maxTime > 0 ? timeRemaining / maxTime : 0f;
            }
        }

        UpdateLocalPlayerStats();
        UpdateAllPlayersDisplay();
    }

    private void OnPhaseChanged(GamePhase phase, int round)
    {
        if (phaseText != null)
        {
            phaseText.text = phase switch
            {
                GamePhase.Lobby => "WAITING FOR PLAYERS",
                GamePhase.UpgradePhase => "CHOOSE UPGRADE",
                GamePhase.CombatPhase => "COMBAT",
                GamePhase.BossPhase => "BOSS FIGHT",
                GamePhase.GameEnd => "GAME END",
                _ => "",
            };
        }

        if (roundText != null)
        {
            roundText.text = $"Round {round}";
        }
    }

    private void OnHealthChanged(HealthChangedEvent @event)
    {
        if (@event.Entity.Equals(_localPlayerEntity))
        {
            UpdateHealthDisplay(@event.CurrentHealth, @event.MaxHealth);
        }
    }

    private void UpdateHealthDisplay(float current, float max)
    {
        if (healthBar != null)
        {
            healthBar.value = max > 0 ? current / max : 0f;
        }

        if (healthText != null)
        {
            healthText.text = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
        }
    }

    private void UpdateLocalPlayerStats()
    {
        if (_localPlayerEntity.Equals(default))
        {
            FindLocalPlayerEntity();
            return;
        }

        if (_world.Components.TryGet(_localPlayerEntity, out PlayerScoreComponent score))
        {
            if (scoreText != null)
            {
                scoreText.text = $"Score: {score.TotalScore}";
            }

            if (killsText != null)
            {
                killsText.text = $"E: {score.EnemyKills} | P: {score.PlayerKills}";
            }
        }
    }

    private void UpdateAllPlayersDisplay()
    {
        if (allPlayersContainer == null || playerStatEntryPrefab == null)
        {
            return;
        }

        // Get all players sorted by score
        List<PlayerDisplayInfo> players = new();

        foreach (var (entity, owner, health) in _world.Components.Query<NetworkOwnerComponent, HealthDataComponent>())
        {
            if (!_world.Components.Has<PlayerTagComponent>(entity))
            {
                continue;
            }

            string playerName = $"Player {owner.ClientId}";
            if (_world.Components.TryGet(entity, out CharacterSelectionComponent charSelect) && charSelect.CharacterData != null)
            {
                playerName = charSelect.CharacterData.characterName;
            }

            int score = 0;
            int playerKills = 0;
            int enemyKills = 0;
            if (_world.Components.TryGet(entity, out PlayerScoreComponent scoreComp))
            {
                score = scoreComp.TotalScore;
                playerKills = scoreComp.PlayerKills;
                enemyKills = scoreComp.EnemyKills;
            }

            players.Add(
                new PlayerDisplayInfo
                {
                    ClientId = owner.ClientId,
                    PlayerName = playerName,
                    Score = score,
                    CurrentHealth = health.CurrentHealth,
                    MaxHealth = health.MaxHealth,
                    IsDead = health.IsDead,
                    PlayerKills = playerKills,
                    EnemyKills = enemyKills,
                }
            );
        }

        // Sort by score descending
        players.Sort((a, b) => b.Score.CompareTo(a.Score));

        // Update UI entries
        for (int i = 0; i < players.Count; i++)
        {
            var player = players[i];

            if (!_playerEntries.TryGetValue(player.ClientId, out GameObject entry))
            {
                entry = Instantiate(playerStatEntryPrefab, allPlayersContainer);
                _playerEntries[player.ClientId] = entry;
            }

            UpdatePlayerEntry(entry, player, i + 1);
        }

        // Remove entries for disconnected players
        List<ulong> toRemove = new();
        foreach (var kvp in _playerEntries)
        {
            if (!players.Exists(p => p.ClientId == kvp.Key))
            {
                Destroy(kvp.Value);
                toRemove.Add(kvp.Key);
            }
        }

        foreach (var id in toRemove)
        {
            _playerEntries.Remove(id);
        }
    }

    private void UpdatePlayerEntry(GameObject entry, PlayerDisplayInfo player, int rank)
    {
        var texts = entry.GetComponentsInChildren<TextMeshProUGUI>();
        if (texts.Length >= 5)
        {
            texts[0].text = $"#{rank}";
            texts[1].text = player.PlayerName;
            texts[2].text = $"{player.Score}";
            texts[3].text = $"P:{player.PlayerKills} E:{player.EnemyKills}";
            texts[4].text = player.IsDead
                ? "DEAD"
                : $"{Mathf.CeilToInt(player.CurrentHealth)}/{Mathf.CeilToInt(player.MaxHealth)}";
        }
        else if (texts.Length >= 4)
        {
            // Fallback for UI with only 4 text fields - combine kills with health
            texts[0].text = $"#{rank}";
            texts[1].text = player.PlayerName;
            texts[2].text = $"{player.Score}";
            texts[3].text = $"P:{player.PlayerKills} E:{player.EnemyKills}";
            texts[4].text = player.IsDead
                ? "DEAD"
                : $"{Mathf.CeilToInt(player.CurrentHealth)}/{Mathf.CeilToInt(player.MaxHealth)}";
        }

        // Highlight local player
        var bg = entry.GetComponent<Image>();
        if (bg != null)
        {
            bg.color =
                player.ClientId == Unity.Netcode.NetworkManager.Singleton.LocalClientId
                    ? new Color(1f, 1f, 0f, 0.3f)
                    : new Color(1f, 1f, 1f, 0.1f);
        }
    }

    private void FindLocalPlayerEntity()
    {
        if (_world == null)
        {
            return;
        }

        foreach (var (entity, owner, player) in _world.Components.Query<NetworkOwnerComponent, PlayerTagComponent>())
        {
            if (owner.IsLocalPlayer)
            {
                _localPlayerEntity = entity;
                Debug.Log($"[GameHUD] Found local player entity: {entity.Id}");

                if (_world.Components.TryGet(entity, out HealthDataComponent health))
                {
                    UpdateHealthDisplay(health.CurrentHealth, health.MaxHealth);
                }

                break;
            }
        }
    }

    private float GetMaxTimeForCurrentPhase()
    {
        if (NetworkGameStateManager.Instance == null)
        {
            return 0f;
        }

        return NetworkGameStateManager.Instance.CurrentPhase switch
        {
            GamePhase.UpgradePhase => GameConstants.UPGRADE_PHASE_DURATION,
            GamePhase.CombatPhase => GameConstants.COMBAT_PHASE_DURATION,
            GamePhase.GameEnd => GameConstants.GAME_END_DURATION,
            _ => 0f,
        };
    }

    private struct PlayerDisplayInfo
    {
        public ulong ClientId;
        public string PlayerName;
        public int Score;
        public float CurrentHealth;
        public float MaxHealth;
        public bool IsDead;
        public int PlayerKills;
        public int EnemyKills;
    }
}
