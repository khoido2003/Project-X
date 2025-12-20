using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public enum UpgradeType
{
    MaxHealth,
    Damage,
    MoveSpeed,
    AttackSpeed,
    HealthRegen,
    CriticalChange,
    AreaDamage,
    LifeStealth,
}

[Serializable]
public class UpgradeDefinition
{
    public UpgradeType type;
    public string upgradeName;
    public string upgradeTime;
    public string description;
    public Sprite icon;
    public float value;
    public bool isPercentage;
}

public struct UpgradeOption : INetworkSerializable
{
    public int UpgradeId;
    public UpgradeType Type;
    public string Name;
    public string Description;
    public float Value; // rolled value after rarity multiplier
    public bool IsPercentage;
    public int RarityTier; // 0=common,1=uncommon,2=rare,3=epic

    public void NetworkSerialize<T>(BufferSerializer<T> serializer)
        where T : IReaderWriter
    {
        serializer.SerializeValue(ref UpgradeId);

        serializer.SerializeValue(ref Type);

        if (serializer.IsWriter)
        {
            serializer.GetFastBufferWriter().WriteValueSafe(Name);
            serializer.GetFastBufferWriter().WriteValueSafe(Description);
        }
        else
        {
            serializer.GetFastBufferReader().ReadValueSafe(out Name);
            serializer.GetFastBufferReader().ReadValueSafe(out Description);
        }
        serializer.SerializeValue(ref Value);
        serializer.SerializeValue(ref IsPercentage);
        serializer.SerializeValue(ref RarityTier);
    }
}

public class NetworkUpgradeSystem : NetworkBehaviour
{
    [Header("Upgrade Pool")]
    [SerializeField]
    private List<UpgradeDefinition> availableUpgrades = new();

    [Header("Configuration")]
    [SerializeField]
    private int upgradeChoicesPerRound = 3;

    [SerializeField]
    private UpgradeCardContainerUI _upgradeCardUI;

    private World _world;

    private Dictionary<int, UpgradeDefinition> _upgradeDatabase = new();
    private int _nextUpgradeId = 0;

    public static NetworkUpgradeSystem Instance { get; private set; }

    private bool _isInitialized = false;

    // Rarity tuning
    private static readonly float[] _rarityWeights = { 0.6f, 0.25f, 0.1f, 0.05f };
    private static readonly float[] _rarityMultipliers = { 1f, 1.25f, 1.5f, 2f };

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

        StartCoroutine(InitializeWhenReady());
    }

    private IEnumerator InitializeWhenReady()
    {
        // Wait for World to be ready
        while (WorldRunner.Instance == null || WorldRunner.Instance.World == null)
        {
            yield return null;
        }

        _world = WorldRunner.Instance.World;

        // Build upgrade database
        foreach (var upgrade in availableUpgrades)
        {
            _upgradeDatabase[_nextUpgradeId] = upgrade;
            _nextUpgradeId++;
        }

        _isInitialized = true;
        Debug.Log($"[UpgradeSystem] Initialized with {_upgradeDatabase.Count} upgrades");
    }

    public void GenerateUpgradesForAllPlayers()
    {
        if (!IsServer || !_isInitialized || _world == null)
        {
            return;
        }

        foreach (var (entity, player, owner) in _world.Components.Query<PlayerTagComponent, NetworkOwnerComponent>())
        {
            GenerateUpgradesForPlayer(entity, owner.ClientId);
        }
    }

    public void GenerateUpgradesForPlayer(EntityId entity, ulong clientId)
    {
        if (!IsServer || !_isInitialized)
        {
            return;
        }

        if (!_world.Components.TryGet(entity, out PlayerUpgradesComponent playerUpgrades))
        {
            playerUpgrades = new PlayerUpgradesComponent();
            _world.Components.Add(entity, playerUpgrades);
        }

        List<UpgradeOption> options = new();
        List<int> availableIds = new(_upgradeDatabase.Keys);

        availableIds.RemoveAll(id =>
        {
            var upgrade = _upgradeDatabase[id];
            return playerUpgrades.AppliedUpgrades.Contains(upgrade.type);
        });

        if (availableIds.Count < upgradeChoicesPerRound)
        {
            Debug.LogWarning($"[UpgradeSystem] Not enough unique upgrades available for player {clientId}");

            // Allow duplicates
            availableIds = new List<int>(_upgradeDatabase.Keys);
        }

        Shuffle(availableIds);

        for (int i = 0; i < Mathf.Min(upgradeChoicesPerRound, availableIds.Count); i++)
        {
            int upgradeId = availableIds[i];
            UpgradeDefinition upgrade = _upgradeDatabase[upgradeId];

            (int rarityTier, float rarityMultiplier) = RollRarity();
            float rolledValue = upgrade.value * rarityMultiplier;

            options.Add(
                new UpgradeOption
                {
                    UpgradeId = upgradeId,
                    Type = upgrade.type,
                    Name = upgrade.upgradeName,
                    Description = upgrade.description,
                    Value = rolledValue,
                    IsPercentage = upgrade.isPercentage,
                    RarityTier = rarityTier,
                }
            );
        }

        SendUpgradeOptionsClientRpc(clientId, options.ToArray());
    }

    ///////////////////////////////////////////////////////////////////////

    #region Upgrade Stats

    private (int rarityTier, float multiplier) RollRarity()
    {
        float roll = UnityEngine.Random.value;
        float cumulative = 0f;
        for (int i = 0; i < _rarityWeights.Length; i++)
        {
            cumulative += _rarityWeights[i];
            if (roll <= cumulative)
            {
                float mult = i < _rarityMultipliers.Length ? _rarityMultipliers[i] : 1f;
                return (i, mult);
            }
        }

        return (0, 1f);
    }

    private void ApplyUpgrades(EntityId entity, int upgradeId, float rolledValue)
    {
        var upgrade = _upgradeDatabase[upgradeId];

        if (!_world.Components.TryGet(entity, out PlayerUpgradesComponent playerUpgrades))
        {
            playerUpgrades = new PlayerUpgradesComponent();
            _world.Components.Add(entity, playerUpgrades);
        }

        if (!playerUpgrades.AppliedUpgrades.Contains(upgrade.type))
        {
            playerUpgrades.AppliedUpgrades.Add(upgrade.type);
        }

        switch (upgrade.type)
        {
            case UpgradeType.MaxHealth:
                ApplyMaxHealthUpgrade(entity, rolledValue, upgrade.isPercentage);
                break;

            case UpgradeType.Damage:
                float prevDamage = playerUpgrades.DamageMultiplier;

                float damageMult = upgrade.isPercentage ? 1f + (rolledValue / 100f) : 1f + rolledValue;

                playerUpgrades.DamageMultiplier *= Mathf.Max(0.01f, damageMult);
                float damageDelta = playerUpgrades.DamageMultiplier / Mathf.Max(0.01f, prevDamage);

                ApplyDamageUpgrade(entity, damageDelta);
                break;

            case UpgradeType.MoveSpeed:
                float prevMove = playerUpgrades.MoveSpeedMultiplier;
                float moveMult = upgrade.isPercentage ? 1f + (rolledValue / 100f) : 1f + rolledValue;

                playerUpgrades.MoveSpeedMultiplier *= Mathf.Max(0.01f, moveMult);

                float moveDelta = playerUpgrades.MoveSpeedMultiplier / Mathf.Max(0.01f, prevMove);

                ApplyMoveSpeedUpgrade(entity, moveDelta);
                break;

            case UpgradeType.AttackSpeed:
                float prevAtk = playerUpgrades.AttackSpeedMultiplier;
                float atkMult = upgrade.isPercentage ? 1f + (rolledValue / 100f) : 1f + rolledValue;

                playerUpgrades.AttackSpeedMultiplier *= Mathf.Max(0.01f, atkMult);

                float atkDelta = playerUpgrades.AttackSpeedMultiplier / Mathf.Max(0.01f, prevAtk);

                ApplyAttackSpeedUpgrade(entity, atkDelta, playerUpgrades.AttackSpeedMultiplier);
                break;

            case UpgradeType.HealthRegen:
                playerUpgrades.HealthRegenPerSecond += rolledValue;
                break;

            case UpgradeType.CriticalChange:
                playerUpgrades.CriticalChance += rolledValue;
                break;

            case UpgradeType.AreaDamage:
                playerUpgrades.AreaDamageRadius += rolledValue;
                break;

            case UpgradeType.LifeStealth:
                playerUpgrades.LifestealPercent += rolledValue;
                break;
        }
    }

    private void ApplyMaxHealthUpgrade(EntityId entity, float value, bool isPercentage)
    {
        if (!_world.Components.TryGet(entity, out HealthDataComponent health))
        {
            return;
        }

        float oldMax = health.MaxHealth;
        float increase = isPercentage ? oldMax * (value / 100f) : value;

        health.MaxHealth += increase;
        health.CurrentHealth += increase;

        _world.Events.Publish(new HealthChangedEvent(entity, health.CurrentHealth, health.MaxHealth));
    }

    private void ApplyDamageUpgrade(EntityId entity, float deltaMultiplier)
    {
        if (!_world.Components.TryGet(entity, out WeaponDataComponent weapon))
        {
            return;
        }

        if (!_world.Components.TryGet(entity, out PlayerUpgradesComponent upgrades))
        {
            return;
        }

        weapon.BaseDamage *= deltaMultiplier;
    }

    private void ApplyMoveSpeedUpgrade(EntityId entity, float deltaMultiplier)
    {
        if (!_world.Components.TryGet(entity, out MovementDataComponent movement))
        {
            return;
        }

        movement.MoveSpeed *= deltaMultiplier;
    }

    private void ApplyAttackSpeedUpgrade(EntityId entity, float deltaMultiplier, float totalMultiplier)
    {
        if (!_world.Components.TryGet(entity, out WeaponDataComponent weapon))
        {
            return;
        }

        if (_world.Components.TryGet(entity, out AttackDataComponent attack))
        {
            attack.AttackSpeedMultiplier = totalMultiplier;
        }
    }

    #endregion

    //////////////////////////////////////////////////////////////////////////

    #region RPCs

    [ServerRpc(RequireOwnership = false)]
    public void SelectUpgradeServerRpc(
        int upgradeId,
        float rolledValue,
        int rarityTier,
        ServerRpcParams rpcParams = default
    )
    {
        if (!_isInitialized)
        {
            Debug.LogError("[UpgradeSystem] System not initialized yet");
            return;
        }

        ulong clientId = rpcParams.Receive.SenderClientId;

        if (!_upgradeDatabase.ContainsKey(upgradeId))
        {
            Debug.LogError($"[UpgradeSystem] Invalid upgradeId: {upgradeId}");
            return;
        }

        EntityId playerEntity = FindPlayerEntityByClientId(clientId);

        if (playerEntity.Equals(default))
        {
            Debug.LogError($"[UpgradeSystem] Could not find player entity for client {clientId}");
            return;
        }

        // Apply upgrade
        ApplyUpgrades(playerEntity, upgradeId, rolledValue);

        // Confirm to client
        ConfirmUpgradeClientRpc(clientId, upgradeId);

        Debug.Log($"[UpgradeSystem] Applied upgrade {upgradeId} to client {clientId}");
    }

    [ClientRpc]
    private void SendUpgradeOptionsClientRpc(
        ulong targetClientId,
        UpgradeOption[] options,
        ClientRpcParams rpcParams = default
    )
    {
        if (NetworkManager.Singleton.LocalClientId != targetClientId)
        {
            return;
        }

        // Show upgrade UI
        if (_upgradeCardUI == null)
        {
            Debug.LogError("UpgradeCardUI is null!");
        }
        _upgradeCardUI.ShowUpgradeOptions(options);
    }

    [ClientRpc]
    private void ConfirmUpgradeClientRpc(ulong targetClientId, int upgradeId)
    {
        if (NetworkManager.Singleton.LocalClientId != targetClientId)
        {
            return;
        }

        // Hide upgrade UI
        if (_upgradeCardUI == null)
        {
            Debug.LogError("UpgradeCardUI is null!");
        }

        _upgradeCardUI.HideUpgradeOptions();
    }

    #endregion

    ////////////////////////////////////////////////////////////////////////////

    #region Utils


    private EntityId FindPlayerEntityByClientId(ulong clientId)
    {
        foreach (var (entity, owner) in _world.Components.Query<NetworkOwnerComponent>())
        {
            if (owner.ClientId == clientId)
            {
                return entity;
            }
        }

        return default;
    }

    private void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);

            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    #endregion
}
