using System.Collections.Generic;
using UnityEngine;

public enum GamePhase
{
    Lobby,
    UpgradePhase,
    CombatPhase,
    BossPhase,
    GameEnd,
}

public class PlayerScoreComponent
{
    public int EnemyKills = 0;
    public int PlayerKills = 0;
    public int BossKills = 0;
    public int TotalScore = 0;
    public EntityId LastAttacker = default;
}

public class PlayerRespawnComponent
{
    public bool IsDead = false;
    public float RespawnTimer = 0;
    public float RespawnDelay = 5f;
    public Vector3 DeathPosition;
}

public class PlayerUpgradesComponent
{
    public float MaxHealthBonus = 0f;
    public float DamageMultiplier = 1f;
    public float MoveSpeedMultiplier = 1f;
    public float AttackSpeedMultiplier = 1f;
    public float HealthRegenPerSecond = 0f;
    public float CriticalChance = 0f;
    public float AreaDamageRadius = 0f;
    public float LifestealPercent = 0f;

    public List<UpgradeType> AppliedUpgrades = new();
}
