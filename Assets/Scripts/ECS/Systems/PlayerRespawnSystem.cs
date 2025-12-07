using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class PlayerRespawnSystem : ISystem
{
    private World _world;

    public void Initialize(World world)
    {
        _world = world;
        _world.Events.Subscribe<EntityDeathEvent>(OnEntityDeath);
    }

    public void Update(float dt)
    {
        if (!NetworkManager.Singleton.IsServer)
        {
            return;
        }

        foreach (
            var (entity, respawn, health) in _world.Components.Query<PlayerRespawnComponent, HealthDataComponent>()
        )
        {
            if (respawn.IsDead)
            {
                respawn.RespawnTimer += dt;

                if (respawn.RespawnTimer >= respawn.RespawnDelay)
                {
                    RespawnPlayer(entity, respawn, health);
                }
            }
        }
    }

    public void FixedUpdate(float dt) { }

    private void RespawnPlayer(EntityId entity, PlayerRespawnComponent respawn, HealthDataComponent health)
    {
        Vector3 spawnPos = GetRespawnPosition();

        health.CurrentHealth = health.MaxHealth;
        health.IsDead = false;

        respawn.IsDead = false;
        respawn.RespawnTimer = 0f;

        if (_world.Components.TryGet(entity, out MovementDataComponent movement))
        {
            movement.IsStunned = false;
        }

        // Move player
        if (_world.Components.TryGet(entity, out TransformComponent trans))
        {
            trans.Position = spawnPos;

            var registry = _world.Services.Resolve<EntityViewRegistry>();
            if (registry.TryGet(entity, out EntityView view))
            {
                view.transform.position = spawnPos;
            }
        }

        // Reset combat state
        if (_world.Components.TryGet(entity, out CombatStateComponent combat))
        {
            combat.CurrentState = CombatState.Idle;
        }

        Debug.Log($"[PlayerRespawn] Player {entity.Id} respawned at {spawnPos}");

        // Broadcast respawn to clients
        if (_world.Components.TryGet(entity, out NetworkSyncComponent sync))
        {
            sync.SyncView.BroadcastPlayerRespawnClientRpc(spawnPos);
        }

        _world.Events.Publish(new HealthChangedEvent(entity, health.CurrentHealth, health.MaxHealth));
        _world.Events.Publish(new PlayerRespawnedEvent(entity));
    }

    private Vector3 GetRespawnPosition()
    {
        SpawnPoint[] spawnPoints = UnityEngine.Object.FindObjectsByType<SpawnPoint>(FindObjectsSortMode.None);
        List<Vector3> playerSpawns = new();

        foreach (var sp in spawnPoints)
        {
            if (sp.type == SpawnType.Player)
            {
                playerSpawns.Add(sp.transform.position);
            }
        }

        if (playerSpawns.Count == 0)
        {
            return Vector3.zero;
        }

        return playerSpawns[UnityEngine.Random.Range(0, playerSpawns.Count)];
    }

    private void OnEntityDeath(EntityDeathEvent @event)
    {
        if (!NetworkManager.Singleton.IsServer)
        {
            return;
        }

        // Handle player death
        if (_world.Components.Has<PlayerTagComponent>(@event.Entity))
        {
            HandlePlayerDeath(@event.Entity);
        }

        // Handle enemy death
        if (_world.Components.Has<EnemyComponent>(@event.Entity))
        {
            HandleEnemyDeath(@event.Entity);
        }
    }

    private void HandlePlayerDeath(EntityId deadPlayer)
    {
        if (!_world.Components.TryGet(deadPlayer, out PlayerRespawnComponent respawn))
        {
            respawn = new PlayerRespawnComponent();
            _world.Components.Add(deadPlayer, respawn);
        }

        if (_world.Components.TryGet(deadPlayer, out TransformComponent trans))
        {
            respawn.DeathPosition = trans.Position;
        }

        respawn.IsDead = true;
        respawn.RespawnTimer = 0f;
        respawn.RespawnDelay = GameConstants.PLAYER_RESPAWN_DELAY;

        // Award killer
        if (_world.Components.TryGet(deadPlayer, out PlayerScoreComponent victimScore))
        {
            if (!victimScore.LastAttacker.Equals(default))
            {
                // Check if attacker is a player
                if (_world.Components.Has<PlayerTagComponent>(victimScore.LastAttacker))
                {
                    AwardPoints(victimScore.LastAttacker, GameConstants.SCORE_PLAYER_KILL, false, true);
                    Debug.Log($"[PlayerRespawn] Player {victimScore.LastAttacker.Id} killed player {deadPlayer.Id}");
                }
            }
        }

        // Broadcast death to clients
        if (_world.Components.TryGet(deadPlayer, out NetworkSyncComponent sync))
        {
            sync.SyncView.BroadcastRespawnTimerClientRpc(GameConstants.PLAYER_RESPAWN_DELAY);
        }

        // Disable player controls during respawn
        if (_world.Components.TryGet(deadPlayer, out MovementDataComponent movement))
        {
            movement.IsStunned = true;
        }
    }

    private void HandleEnemyDeath(EntityId deadEnemy)
    {
        if (_world.Components.TryGet(deadEnemy, out EnemyComponent enemy))
        {
            if (
                !enemy.LastAttacker.Equals(default)
                && Time.time - enemy.LastDamageTime < GameConstants.DAMAGE_ATTRIBUTION_TIMEOUT
            )
            {
                int points = enemy.IsBoss ? GameConstants.SCORE_BOSS_KILL : GameConstants.SCORE_ENEMY_KILL;
                AwardPoints(enemy.LastAttacker, points, enemy.IsBoss, false);
            }
        }
    }

    private void AwardPoints(EntityId killer, int points, bool isBoss, bool isPlayerKill)
    {
        if (!_world.Components.TryGet(killer, out PlayerScoreComponent score))
        {
            score = new PlayerScoreComponent();
            _world.Components.Add(killer, score);
        }

        score.TotalScore += points;

        if (isBoss)
        {
            score.BossKills++;
        }
        else if (isPlayerKill)
        {
            score.PlayerKills++;
        }
        else
        {
            score.EnemyKills++;
        }

        Debug.Log($"[Score] Player {killer.Id} earned {points} points. Total: {score.TotalScore}");

        // Broadcast score update
        if (_world.Components.TryGet(killer, out NetworkOwnerComponent owner))
        {
            BroadcastScoreUpdate(owner.ClientId, score.TotalScore);
        }
    }

    private void BroadcastScoreUpdate(ulong clientId, int newScore)
    {
        var gameStateManager = NetworkGameStateManager.Instance;
        if (gameStateManager != null)
        {
            Debug.Log($"[Score] Broadcasting score update to client {clientId}: {newScore}");
        }
    }

    public void Shutdown()
    {
        _world.Events.Unsubscribe<EntityDeathEvent>(OnEntityDeath);
    }
}
