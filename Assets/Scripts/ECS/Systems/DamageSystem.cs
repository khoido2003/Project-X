using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class DamageSystem : ISystem
{
    private World _world;
    private readonly Dictionary<EntityId, (float value, float expires)> _defenseBuffs = new();

    public void Initialize(World world)
    {
        _world = world;
        _world.Events.Subscribe<DamageEvent>(OnDamage);
        _world.Events.Subscribe<ApplyBuffEvent>(OnApplyBuff);
    }

    public void Update(float dt)
    {
        if (_defenseBuffs.Count == 0)
        {
            return;
        }

        List<EntityId> toRemove = null;
        foreach (var kvp in _defenseBuffs)
        {
            if (Time.time >= kvp.Value.expires)
            {
                toRemove ??= new List<EntityId>();
                toRemove.Add(kvp.Key);
            }
        }

        if (toRemove != null)
        {
            foreach (var id in toRemove)
            {
                _defenseBuffs.Remove(id);
            }
        }
    }

    public void FixedUpdate(float dt) { }

    private void OnDamage(DamageEvent @event)
    {
        if (!NetworkManager.Singleton.IsServer)
        {
            return;
        }

        if (!_world.Components.TryGet(@event.Target, out HealthDataComponent health))
        {
            Debug.LogWarning($"[DamageSystem] Target {@event.Target} missing HealthDataComponent");
            return;
        }

        if (health.IsDead)
        {
            return;
        }

        float actualDamage = @event.Amount;

        // Apply defense buffs (shield)
        if (_defenseBuffs.TryGetValue(@event.Target, out var buff) && Time.time < buff.expires)
        {
            float reduction = Mathf.Clamp01(buff.value);
            actualDamage *= 1f - reduction;
        }
        else if (_defenseBuffs.ContainsKey(@event.Target))
        {
            _defenseBuffs.Remove(@event.Target);
        }

        // Apply critical & lifesteal for attackers with upgrades
        if (_world.Components.TryGet(@event.Attacker, out PlayerUpgradesComponent attackerUpgrades))
        {
            // Crit check (simple 2x)
            float critChance = attackerUpgrades.CriticalChance / 100f;
            if (UnityEngine.Random.value < critChance)
            {
                actualDamage *= 2f;
            }
        }
        health.CurrentHealth -= actualDamage;

        if (health.CurrentHealth < 0)
        {
            health.CurrentHealth = 0;
        }

        _world.Components.Add(@event.Target, health);

        // Track attacker for kill counts
        if (!@event.Attacker.Equals(default))
        {
            if (_world.Components.Has<EnemyComponent>(@event.Target))
            {
                var enemy = _world.Components.Get<EnemyComponent>(@event.Target);
                enemy.LastAttacker = @event.Attacker;
                enemy.LastDamageTime = Time.time;
            }
            else if (_world.Components.Has<PlayerTagComponent>(@event.Target))
            {
                if (!_world.Components.TryGet(@event.Target, out PlayerScoreComponent score))
                {
                    score = new PlayerScoreComponent();
                    _world.Components.Add(@event.Target, score);
                }

                score.LastAttacker = @event.Attacker;
            }
        }

        _world.Events.Publish(new HealthChangedEvent(@event.Target, health.CurrentHealth, health.MaxHealth));

        // Play damage/hurt sound on target
        Vector3 hitPosition = Vector3.zero;
        var registry = _world.Services.Resolve<EntityViewRegistry>();
        if (registry.TryGet(@event.Target, out EntityView targetView))
        {
            hitPosition = targetView.transform.position;
        }

        // Publish impact sound at hit position (from attacker's profile if available)
        if (!@event.Attacker.Equals(default))
        {
            _world.Events.Publish(new AudioCueEvent(@event.Attacker, AudioCueType.Impact, hitPosition));
        }

        // Lifesteal after damage dealt (only if attacker exists and is alive)
        if (
            @event.Attacker != default
            && _world.Components.TryGet(@event.Attacker, out PlayerUpgradesComponent atkUpgrades)
        )
        {
            if (
                atkUpgrades.LifestealPercent > 0f
                && _world.Components.TryGet(@event.Attacker, out HealthDataComponent attackerHealth)
            )
            {
                float heal = actualDamage * (atkUpgrades.LifestealPercent / 100f);
                if (heal > 0f && !attackerHealth.IsDead)
                {
                    attackerHealth.CurrentHealth = Mathf.Min(
                        attackerHealth.CurrentHealth + heal,
                        attackerHealth.MaxHealth
                    );
                    _world.Events.Publish(
                        new HealthChangedEvent(@event.Attacker, attackerHealth.CurrentHealth, attackerHealth.MaxHealth)
                    );
                }
            }
        }

        // Broadcast damage visual to all clients
        Vector3 hitPoint = Vector3.zero;

        if (registry.TryGet(@event.Target, out EntityView view))
        {
            hitPoint = view.transform.position;
        }

        // Check if it's a player or enemy
        if (_world.Components.Has<PlayerTagComponent>(@event.Target))
        {
            if (_world.Components.TryGet(@event.Target, out NetworkSyncComponent sync))
            {
                sync.SyncView.BroadcastDamageVisualClientRpc(actualDamage, hitPoint);
            }
        }
        else if (_world.Components.Has<EnemyComponent>(@event.Target))
        {
            if (_world.Components.TryGet(@event.Target, out NetworkObjectComponent netObj))
            {
                if (netObj.NetworkObject != null && netObj.NetworkObject.IsSpawned)
                {
                    var enemySync = netObj.NetworkObject.GetComponent<EnemyNetworkSyncView>();
                    if (enemySync != null && enemySync.IsSpawned)
                    {
                        enemySync.BroadcastDamageVisualClientRpc(actualDamage, hitPoint);
                    }
                }
            }
        }
    }

    public void Shutdown()
    {
        _world.Events.Unsubscribe<DamageEvent>(OnDamage);
        _world.Events.Unsubscribe<ApplyBuffEvent>(OnApplyBuff);
    }

    private void OnApplyBuff(ApplyBuffEvent @event)
    {
        if (!NetworkManager.Singleton.IsServer)
        {
            return;
        }

        if (@event.BuffType == BuffType.DefenseBoost)
        {
            float value = Mathf.Clamp01(@event.Value);
            _defenseBuffs[@event.Target] = (value, Time.time + @event.Duration);
        }
    }
}
