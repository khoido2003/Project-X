using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class NetworkGameStateManager : NetworkBehaviour
{
    [Header("Configuration")]
    [SerializeField]
    private int maxRounds = 3;

    [SerializeField]
    private float upgradePhaseDuration = 15f;

    [SerializeField]
    private float combatPhaseDuration = 60f;

    [SerializeField]
    private int minPlayers = 1;

    [SerializeField]
    private GameResultsUI resultsUI;

    [Header("Audio Configuration")]
    [SerializeField]
    private VoiceoverConfig voiceoverConfig;

    private NetworkVariable<GamePhase> _netCurrentPhase = new(
        GamePhase.Lobby,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private NetworkVariable<int> _netCurrentRound = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private NetworkVariable<float> _netPhaseTimeRemaining = new(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private NetworkVariable<bool> _netBossSpawned = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private World _world;

    [SerializeField]
    private WaveManager _waveManager;

    private float _phaseTimer;
    private bool _isInitialized = false;
    private int _lastCountdownPlayed = -1; // Track last countdown number played

    public static NetworkGameStateManager Instance { get; private set; }

    public event Action<GamePhase, int> OnPhaseChanged;
    public event Action<float> OnPhaseTimerUpdate;

    public GamePhase CurrentPhase => _netCurrentPhase.Value;
    public int CurrentRound => _netCurrentRound.Value;
    public float PhaseTimeRemaining => _netPhaseTimeRemaining.Value;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // Wait for WorldRunner to initialize
        StartCoroutine(InitializeWhenReady());

        if (!IsServer)
        {
            _netCurrentPhase.OnValueChanged += OnPhaseChangedClient;
            _netCurrentRound.OnValueChanged += OnRoundChangedClient;
        }

        Debug.Log("[NetworkGameStateManager] Network spawned, waiting for World initialization");
    }

    private IEnumerator InitializeWhenReady()
    {
        // Wait until WorldRunner and World are ready
        while (WorldRunner.Instance == null || WorldRunner.Instance.World == null)
        {
            yield return null;
        }

        _world = WorldRunner.Instance.World;

        if (_waveManager == null)
        {
            Debug.LogError("[NetworkGameStateManager]: Wave Manager not found!");
        }

        if (IsServer)
        {
            _world.Events.Subscribe<EntityDeathEvent>(OnEntityDeath);
        }

        _isInitialized = true;
        Debug.Log("[NetworkGameStateManager] Initialized successfully");
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();

        if (!IsServer)
        {
            _netCurrentPhase.OnValueChanged -= OnPhaseChangedClient;
            _netCurrentRound.OnValueChanged -= OnRoundChangedClient;
        }

        if (IsServer && _world != null)
        {
            _world.Events.Unsubscribe<EntityDeathEvent>(OnEntityDeath);
        }
    }

    private void Update()
    {
        if (!IsServer || !_isInitialized || _world == null)
        {
            return;
        }

        _phaseTimer += Time.deltaTime;
        _netPhaseTimeRemaining.Value = Mathf.Max(0f, GetPhaseDuration() - _phaseTimer);

        switch (_netCurrentPhase.Value)
        {
            case GamePhase.Lobby:
                UpdateLobbyPhase();
                break;
            case GamePhase.UpgradePhase:
                UpdateUpgradePhase();
                break;
            case GamePhase.CombatPhase:
                UpdateCombatPhase();
                break;
            case GamePhase.BossPhase:
                UpdateBossPhase();
                break;
            case GamePhase.GameEnd:
                UpdateGameEndPhase();
                break;
        }
    }

    ////////////////////////////////////////////////////////////////////////

    #region PHASE Transition


    private void TransitionToPhase(GamePhase newPhase)
    {
        if (!IsServer || !_isInitialized)
        {
            return;
        }

        _netCurrentPhase.Value = newPhase;
        _phaseTimer = 0f;
        _lastCountdownPlayed = -1; // Reset countdown when phase changes

        _netPhaseTimeRemaining.Value = GetPhaseDuration();

        switch (newPhase)
        {
            case GamePhase.UpgradePhase:

                var upgradeSystem = NetworkUpgradeSystem.Instance;
                upgradeSystem?.GenerateUpgradesForAllPlayers();

                BroadcastPhaseStartClientRpc(newPhase, _netCurrentRound.Value + 1, upgradePhaseDuration);
                break;

            case GamePhase.CombatPhase:

                SpawnWaveForRound(_netCurrentRound.Value + 1);
                BroadcastPhaseStartClientRpc(newPhase, _netCurrentRound.Value + 2, upgradePhaseDuration);
                break;

            case GamePhase.BossPhase:
                SpawnBoss();
                BroadcastPhaseStartClientRpc(newPhase, _netCurrentRound.Value + 1, upgradePhaseDuration);
                break;

            case GamePhase.GameEnd:
                // Destroy all enemies and players when match ends
                DestroyAllEntities();
                CalculateAndBroadcastResults();
                BroadcastPhaseStartClientRpc(newPhase, _netCurrentRound.Value + 1, upgradePhaseDuration);
                break;
        }
    }

    private float GetPhaseDuration()
    {
        return _netCurrentPhase.Value switch
        {
            GamePhase.UpgradePhase => upgradePhaseDuration,
            GamePhase.CombatPhase => combatPhaseDuration,
            GamePhase.GameEnd => 10f,
            _ => 0f,
        };
    }

    #endregion

    /////////////////////////////////////////////////////////////////////////

    #region RPCs

    [ClientRpc]
    private void BroadcastPhaseStartClientRpc(GamePhase newPhase, int round, float duration)
    {
        OnPhaseChanged?.Invoke(newPhase, round);
        OnPhaseTimerUpdate?.Invoke(duration);

        // Play phase voiceover
        PlayPhaseVoiceover(newPhase, round);
    }

    private void PlayPhaseVoiceover(GamePhase phase, int round)
    {
        if (voiceoverConfig == null || AudioService.Instance == null)
        {
            return;
        }

        AudioClip clip = null;
        switch (phase)
        {
            case GamePhase.UpgradePhase:
                clip = voiceoverConfig.upgradePhase;
                break;
            case GamePhase.CombatPhase:
                // Only play round announcement, not combatPhase (to avoid overlap)
                if (voiceoverConfig.roundAnnouncement != null && round > 0)
                {
                    clip = voiceoverConfig.roundAnnouncement;
                }
                else
                {
                    clip = voiceoverConfig.combatPhase;
                }
                break;
            case GamePhase.BossPhase:
                clip = voiceoverConfig.bossFight;
                break;
            case GamePhase.GameEnd:
                // Don't play gameOver here - it will be played in results
                return;
        }

        if (clip != null)
        {
            AudioHelper.PlaySound(clip, AudioCategory.UI, voiceoverConfig.voiceoverVolume);
        }
    }

    [ClientRpc]
    private void BroadcastScoreUpdateClientRpc(ulong clientId, int newScore)
    {
        if (NetworkManager.Singleton.LocalClientId == clientId)
        {
            Debug.Log($"[Client] Score updated:  {newScore}");
        }
    }

    [ClientRpc]
    private void BroadcastGameResultsClientRpc(PlayerResult[] results)
    {
        Debug.Log($"[CLient] Received game results with {results.Length} players");

        // Play victory/defeat voiceover based on results with a delay to avoid overlap with final round announcement
        if (voiceoverConfig != null && AudioService.Instance != null)
        {
            StartCoroutine(PlayGameEndVoiceoverDelayed(results));
        }

        if (resultsUI != null)
        {
            resultsUI.DisplayResults(results);
        }
        else
        {
            Debug.LogError("[Client] GameResultsUI not found!");
        }
    }

    private IEnumerator PlayGameEndVoiceoverDelayed(PlayerResult[] results)
    {
        // Wait a bit to ensure any final round announcements have finished
        yield return new WaitForSeconds(2f);

        // Determine if local player won (simplified - check if they're in top 3)
        // You can customize this logic based on your game rules
        bool isVictory = false;
        if (results.Length > 0)
        {
            // Check if local player is first (or in top positions)
            ulong localClientId = NetworkManager.Singleton.LocalClientId;
            for (int i = 0; i < Mathf.Min(3, results.Length); i++)
            {
                if (results[i].ClientId == localClientId)
                {
                    isVictory = true;
                    break;
                }
            }
        }

        // Only play victory or defeat, not gameOver (to avoid overlap)
        AudioClip resultClip = isVictory ? voiceoverConfig.victory : voiceoverConfig.defeat;
        if (resultClip != null && AudioService.Instance != null)
        {
            AudioHelper.PlaySound(resultClip, AudioCategory.UI, voiceoverConfig.voiceoverVolume);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void ResetGameServerRpc()
    {
        if (!IsServer)
        {
            return;
        }

        _netCurrentPhase.Value = GamePhase.Lobby;
        _netCurrentRound.Value = 0;
        _phaseTimer = 0f;
        _netBossSpawned.Value = false;

        foreach (var (entity, score) in _world.Components.Query<PlayerScoreComponent>())
        {
            score.EnemyKills = 0;
            score.PlayerKills = 0;
            score.BossKills = 0;
            score.TotalScore = 0;
        }

        Debug.Log("[NetworkGameState] Game reset");
    }

    #endregion

    ////////////////////////////////////////////////////////////////////////

    #region PHASE Update

    private void UpdateGameEndPhase()
    {
        float maxPhaseTimer = 10f;

        if (_phaseTimer >= maxPhaseTimer)
        {
            ResetGameServerRpc();
        }
    }

    private void UpdateBossPhase()
    {
        if (_netBossSpawned.Value)
        {
            // Check if boss is dead
            int bossCount = CountBosses();
            if (bossCount == 0)
            {
                TransitionToPhase(GamePhase.GameEnd);
            }
        }
    }

    private void UpdateCombatPhase()
    {
        int enemyCount = CountAliveEnemies();

        // Check if this is the final round before boss phase
        bool isFinalRound = _netCurrentRound.Value + 1 >= maxRounds;

        // Play countdown voiceover when phase is ending (but skip if it's the final round to avoid overlap with game end)
        float timeRemaining = combatPhaseDuration - _phaseTimer;
        if (timeRemaining <= 3f && timeRemaining > 0f && !isFinalRound)
        {
            int countdownNumber = Mathf.CeilToInt(timeRemaining);
            if (countdownNumber != _lastCountdownPlayed && voiceoverConfig != null)
            {
                _lastCountdownPlayed = countdownNumber;
                AudioClip countdownClip = voiceoverConfig.GetCountdownClip(countdownNumber);
                if (countdownClip != null && AudioService.Instance != null)
                {
                    AudioHelper.PlaySound(countdownClip, AudioCategory.UI, voiceoverConfig.voiceoverVolume);
                }
            }
        }

        // Phase ends when time runs out OR all enemies dead
        if (_phaseTimer >= combatPhaseDuration || enemyCount == 0)
        {
            _lastCountdownPlayed = -1; // Reset countdown
            _netCurrentRound.Value++;

            if (_netCurrentRound.Value >= maxRounds)
            {
                TransitionToPhase(GamePhase.BossPhase);
            }
            else
            {
                TransitionToPhase(GamePhase.UpgradePhase);
            }
        }
    }

    private void UpdateUpgradePhase()
    {
        // Play countdown voiceover when phase is ending
        float timeRemaining = upgradePhaseDuration - _phaseTimer;
        if (timeRemaining <= 3f && timeRemaining > 0f)
        {
            int countdownNumber = Mathf.CeilToInt(timeRemaining);
            if (countdownNumber != _lastCountdownPlayed && voiceoverConfig != null)
            {
                _lastCountdownPlayed = countdownNumber;
                AudioClip countdownClip = voiceoverConfig.GetCountdownClip(countdownNumber);
                if (countdownClip != null && AudioService.Instance != null)
                {
                    AudioHelper.PlaySound(countdownClip, AudioCategory.UI, voiceoverConfig.voiceoverVolume);
                }
            }
        }

        if (_phaseTimer >= upgradePhaseDuration)
        {
            _lastCountdownPlayed = -1; // Reset countdown
            TransitionToPhase(GamePhase.CombatPhase);
        }
    }

    private void UpdateLobbyPhase()
    {
        int playerCount = CountAlivePlayers();

        float maxPhaseTimer = 5f;

        if (playerCount >= minPlayers && _phaseTimer >= maxPhaseTimer)
        {
            TransitionToPhase(GamePhase.UpgradePhase);
        }
    }

    #endregion


    /////////////////////////////////////////////////////////////////

    #region Scoring

    private void OnEntityDeath(EntityDeathEvent @event)
    {
        if (!IsServer)
        {
            return;
        }

        // PlayerRespawnSystem handle kill attribution
    }

    private void AwardPoints(EntityId player, int points, bool isBoss)
    {
        if (!_world.Components.TryGet(player, out PlayerScoreComponent score))
        {
            score = new PlayerScoreComponent();

            _world.Components.Add(player, score);
        }

        score.TotalScore += points;

        if (isBoss)
        {
            score.BossKills++;
        }
        else
        {
            score.EnemyKills++;
        }

        Debug.Log($"[Score] Player {player.Id} earned {points} points. Total: {score.TotalScore}");

        if (_world.Components.TryGet(player, out NetworkOwnerComponent owner))
        {
            BroadcastScoreUpdateClientRpc(owner.ClientId, score.TotalScore);
        }
    }

    private void CalculateAndBroadcastResults()
    {
        List<PlayerResult> results = new();

        foreach (var (entity, score, owner) in _world.Components.Query<PlayerScoreComponent, NetworkOwnerComponent>())
        {
            string playerName = $"Player_{owner.ClientId}";
            if (_world.Components.TryGet(entity, out CharacterSelectionComponent characterSelection))
            {
                playerName = characterSelection.CharacterData.characterName;
            }

            results.Add(
                new PlayerResult
                {
                    ClientId = owner.ClientId,
                    PlayerName = playerName,
                    TotalScore = score.TotalScore,
                    EnemyKills = score.EnemyKills,
                    PlayerKills = score.PlayerKills,
                    BossKills = score.BossKills,
                }
            );
        }

        results.Sort((a, b) => b.TotalScore.CompareTo(a.TotalScore));

        BroadcastGameResultsClientRpc(results.ToArray());

        Debug.Log("=== GAME RESULTS ===");
        for (int i = 0; i < results.Count; i++)
        {
            Debug.Log($"{i + 1}. {results[i].PlayerName} - Score: {results[i].TotalScore}");
        }
    }

    #endregion


    //////////////////////////////////////////////////////////////////

    #region Enemy Spawning

    private void SpawnWaveForRound(int round)
    {
        if (_waveManager == null)
        {
            Debug.LogError("[NetworkGameState] WaveManager is null");
            return;
        }

        _waveManager.SpawnWave(round);
    }

    private void SpawnBoss()
    {
        if (_waveManager == null)
        {
            Debug.LogError("[NetworkGameState] WaveManager is null");
            return;
        }
        _waveManager.SpawnBoss();
        _netBossSpawned.Value = true;
    }

    #endregion

    ///////////////////////////////////////////////////////////////////

    #region Client callbacks


    private void OnRoundChangedClient(int previousValue, int newValue)
    {
        Debug.Log($"[Client] Round changed from {previousValue} to {newValue}");
    }

    private void OnPhaseChangedClient(GamePhase previousValue, GamePhase newValue)
    {
        Debug.Log($"[Client] Phase changed from {previousValue} to {newValue}");
        // NOTE: Do NOT invoke OnPhaseChanged here!
        // BroadcastPhaseStartClientRpc already handles this with the correct round number.
        // The NetworkVariable update may arrive after the RPC, overwriting the correct round with stale data.
    }

    #endregion

    ////////////////////////////////////////////////////////////////

    #region Utils

    private int CountAlivePlayers()
    {
        int cnt = 0;
        foreach (var (entity, player, health) in _world.Components.Query<PlayerTagComponent, HealthDataComponent>())
        {
            if (!health.IsDead)
            {
                cnt++;
            }
        }
        return cnt;
    }

    private int CountAliveEnemies()
    {
        int count = 0;
        foreach (var (entity, enemy, health) in _world.Components.Query<EnemyComponent, HealthDataComponent>())
        {
            if (!health.IsDead && enemy.CurrentState != EnemyState.Dead && !enemy.IsBoss)
            {
                count++;
            }
        }
        return count;
    }

    private int CountBosses()
    {
        int count = 0;
        foreach (var (entity, enemy, health) in _world.Components.Query<EnemyComponent, HealthDataComponent>())
        {
            if (enemy.IsBoss && !health.IsDead && enemy.CurrentState != EnemyState.Dead)
            {
                count++;
            }
        }
        return count;
    }

    #endregion

    //////////////////////////////////////////////////////////////////

    #region Entity Cleanup

    private void DestroyAllEntities()
    {
        if (!IsServer || _world == null)
        {
            return;
        }

        // Destroy all enemies
        List<EntityId> enemiesToDestroy = new();
        foreach (var (entity, enemy) in _world.Components.Query<EnemyComponent>())
        {
            enemiesToDestroy.Add(entity);
        }

        foreach (var entity in enemiesToDestroy)
        {
            if (_world.Components.TryGet(entity, out NetworkObjectComponent netObj))
            {
                if (netObj.NetworkObject != null && netObj.NetworkObject.IsSpawned)
                {
                    netObj.NetworkObject.Despawn(true);
                }
            }
        }

        // Destroy all players
        List<EntityId> playersToDestroy = new();
        foreach (var (entity, player) in _world.Components.Query<PlayerTagComponent>())
        {
            playersToDestroy.Add(entity);
        }

        foreach (var entity in playersToDestroy)
        {
            if (_world.Components.TryGet(entity, out NetworkObjectComponent netObj))
            {
                if (netObj.NetworkObject != null && netObj.NetworkObject.IsSpawned)
                {
                    netObj.NetworkObject.Despawn(true);
                }
            }
        }

        Debug.Log("[NetworkGameStateManager] Destroyed all enemies and players on match end");
    }

    #endregion
}

[Serializable]
public struct PlayerResult : INetworkSerializable
{
    public ulong ClientId;
    public string PlayerName;
    public int TotalScore;
    public int EnemyKills;
    public int PlayerKills;
    public int BossKills;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer)
        where T : IReaderWriter
    {
        serializer.SerializeValue(ref ClientId);

        if (serializer.IsWriter)
        {
            serializer.GetFastBufferWriter().WriteValueSafe(PlayerName);
        }
        else
        {
            serializer.GetFastBufferReader().ReadValueSafe(out PlayerName);
        }

        serializer.SerializeValue(ref TotalScore);
        serializer.SerializeValue(ref EnemyKills);
        serializer.SerializeValue(ref PlayerKills);
        serializer.SerializeValue(ref BossKills);
    }
}
