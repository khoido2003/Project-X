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

        _netPhaseTimeRemaining.Value = GetPhaseDuration();

        Debug.Log($"NetworkGameState: Transitioning to {newPhase}, Round {_netCurrentPhase.Value + 1}");

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
        Debug.Log($"[Client] Phase changed to {newPhase}, Round {round}");
        OnPhaseChanged?.Invoke(newPhase, round);
        OnPhaseTimerUpdate?.Invoke(duration);
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

        // TODO: update UI here
        if (resultsUI != null)
        {
            resultsUI.DisplayResults(results);
        }
        else
        {
            Debug.LogError("[Client] GameResultsUI not found!");
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

        // Phase ends when time runs out OR all enemies dead
        if (_phaseTimer >= combatPhaseDuration || enemyCount == 0)
        {
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
        if (_phaseTimer >= upgradePhaseDuration)
        {
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
        OnPhaseChanged?.Invoke(newValue, _netCurrentRound.Value);
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
