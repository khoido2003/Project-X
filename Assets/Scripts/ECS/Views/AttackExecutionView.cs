using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class AttackExecutionView : EntityView
{
    [SerializeField]
    private Transform spawnTransform;

    private World _world;
    private EntityViewRegistry _registry;
    private bool _isInitialized;

    private readonly Dictionary<EntityId, HashSet<EntityId>> _attackHitCache = new();

    public override void Bind(World world, EntityId entity)
    {
        base.Bind(world, entity);

        _world = world;
        _registry = world.Services.Resolve<EntityViewRegistry>();

        _world.Events.Subscribe<AttackExecutionRequestEvent>(OnExecuteAttack);
        _world.Events.Subscribe<AnimationEventRelayEvent>(OnAnimationRelay);
        _world.Events.Subscribe<EntityDeathEvent>(OnEntityDeath);
        _world.Events.Subscribe<PlayerRespawnedEvent>(OnPlayerRespawned);
        _world.Events.Subscribe<EntityDestroyedEvent>(OnEntityDestroyed);
        _world.Events.Subscribe<AttackPressedInputEvent>(OnAttackStart);

        // Clear cache on bind to ensure fresh state for new/reused entities
        _attackHitCache.Clear();

        _isInitialized = true;
    }

    private void OnExecuteAttack(AttackExecutionRequestEvent @event)
    {
        // Only server executes attack hit detection
        if (!NetworkManager.Singleton.IsServer)
        {
            return;
        }

        if (!_isInitialized || _registry == null)
        {
            return;
        }

        if (!@event.Attacker.Equals(EntityInstance))
        {
            return;
        }

        if (!_registry.TryGet(@event.Attacker, out EntityView attackerView))
        {
            Debug.LogWarning($"[AttackExecutionView] Could not find EntityView for attacker {EntityInstance.Id}");
            return;
        }
        Transform attackerTf = attackerView.transform;

        switch (@event.Type)
        {
            case AttackExecutionType.Melee:
                ExecuteMeleeAttack(@event, attackerTf);
                break;
            case AttackExecutionType.Projectile:
                ExecuteProjectileAttack(@event, attackerTf);
                break;
            case AttackExecutionType.Area:
                ExecuteAreaAttack(@event, attackerTf);
                break;
            case AttackExecutionType.Beam:
                ExecuteBeamAttack(@event, attackerTf);
                break;
            case AttackExecutionType.Custom:
                ExecuteCustomAttack(@event, attackerTf);
                break;
        }
    }

    // Track the current attack instance to determine when a new attack starts
    private int _lastAttackInstance = 0;
    private int _currentAttackInstance = 0;
    
    private void ExecuteMeleeAttack(AttackExecutionRequestEvent @event, Transform attackerTf)
    {
        // Increment attack instance counter - each ATTACK_HIT is a new attack
        _currentAttackInstance++;
        
        // CRITICAL FIX: Since each ExecuteMeleeAttack call is a new attack (ATTACK_HIT fires once per attack),
        // we should start with a fresh cache. Clear any stale entries from previous attacks.
        // This fixes the bug where dying mid-attack or mech transformation causes ATTACK_END to be missed.
        if (!_attackHitCache.TryGetValue(@event.Attacker, out var damagedEntities))
        {
            damagedEntities = new HashSet<EntityId>();
            _attackHitCache[@event.Attacker] = damagedEntities;
        }
        else
        {
            // Cache exists from a previous attack - clear it for this new attack
            damagedEntities.Clear();
        }

        Vector3 origin = attackerTf.position + attackerTf.forward * 0.5f;
        float radius = @event.Range * 0.5f;

        Collider[] hits = Physics.OverlapSphere(origin, radius);



        foreach (Collider hit in hits)
        {
            if (!hit.TryGetComponent(out EntityView targetView))
            {
                continue;
            }

            EntityId targetEntity = targetView.EntityInstance;
            
            if (targetEntity.Equals(@event.Attacker))
            {
                continue;
            }

            if (damagedEntities.Contains(targetEntity))
            {
                continue;
            }
            
            damagedEntities.Add(targetEntity);

            _world.Events.Publish(
                new DamageEvent
                {
                    Attacker = @event.Attacker,
                    Target = targetEntity,
                    Amount = @event.Damage,
                }
            );

            Vector3 impactPos = hit.ClosestPoint(origin);
            Vector3 impactNormal = (origin - impactPos).normalized;

            // Server spawns impact effect locally
            if (@event.ImpactEffect)
            {
                var impactGo = Instantiate(@event.ImpactEffect, impactPos, Quaternion.identity);
                Destroy(impactGo, 2f); // CRITICAL: Cleanup VFX to prevent memory leak
            }

            // Broadcast to all clients to spawn impact effect
            if (_world.Components.TryGet(@event.Attacker, out NetworkSyncComponent sync) && sync.SyncView != null)
            {
                sync.SyncView.BroadcastMeleeHitClientRpc(impactPos, impactNormal);
            }

            // Play impact sound at hit position
            _world.Events.Publish(new AudioCueEvent(@event.Attacker, SoundType.Impact, impactPos));
        }
    }

    private void ExecuteProjectileAttack(AttackExecutionRequestEvent @event, Transform attackerTf)
    {
        if (@event.ProjectilePrefab == null)
        {
            Debug.LogError($"No projectile prefab found!");
            return;
        }

        // Trigger aiming rig for ranged attacks
        TriggerAimingRigForAttack(@event, attackerTf);

        // Try to find ProjectileSpawnPos component on attacker or its children
        Transform spawnTransform = attackerTf;
        ProjectileSpawnPos spawnPosComponent = attackerTf.GetComponentInChildren<ProjectileSpawnPos>();

        if (spawnPosComponent != null)
        {
            spawnTransform = spawnPosComponent.transform;
        }

        // Calculate spawn position first
        Vector3 spawnPos;
        if (spawnPosComponent != null)
        {
            spawnPos = spawnTransform.position;
            // Apply offset relative to spawn transform
            spawnPos += spawnTransform.TransformDirection(@event.SpawnOffset);
        }
        else
        {
            // Default spawn position - use attacker position with height offset
            spawnPos = attackerTf.position + new Vector3(0f, 1.3f, 0f);

            Vector3 tempDir =
                @event.Direction.sqrMagnitude < 0.0001f ? attackerTf.forward : @event.Direction.normalized;
            tempDir.y = 0f;
            tempDir = tempDir.normalized;

            // Apply offset in world space relative to direction
            if (@event.SpawnOffset.sqrMagnitude > 0.0001f)
            {
                spawnPos += Quaternion.LookRotation(tempDir, Vector3.up) * @event.SpawnOffset;
            }
        }

        //Calculate direction FROM spawn position TO target point
        // The @event.Direction was calculated from character center, but we need direction from gun muzzle
        // Use a large distance to minimize parallax error from spawn offset
        Vector3 forwardDir;
        if (@event.Direction.sqrMagnitude > 0.0001f)
        {
            // Calculate target point far in the aim direction
            // Using a large distance (100f) makes the spawn offset negligible
            Vector3 targetPoint = attackerTf.position + @event.Direction.normalized * 100f;
            targetPoint.y = spawnPos.y; // Keep on same height as spawn

            forwardDir = (targetPoint - spawnPos).normalized;
            forwardDir.y = 0f;
            forwardDir = forwardDir.normalized;
        }
        else
        {
            forwardDir = attackerTf.forward;
            forwardDir.y = 0f;
            forwardDir = forwardDir.normalized;
        }

        if (forwardDir.sqrMagnitude < 0.0001f)
        {
            forwardDir = attackerTf.forward;
            forwardDir.y = 0f;
            forwardDir = forwardDir.normalized;
        }

        Quaternion spawnRot = Quaternion.LookRotation(forwardDir, Vector3.up);
        var pool = _world.Services.Resolve<ObjectPoolService>();

        GameObject projectileGO = pool.Get(@event.ProjectilePrefab, spawnPos, spawnRot);

        if (!projectileGO.TryGetComponent(out ProjectileView projectile))
        {
            projectile = projectileGO.AddComponent<ProjectileView>();
        }

        projectile.Initialize(
            _world,
            @event.Attacker,
            @event.Damage,
            @event.ProjectileSpeed,
            @event.ProjectileLifetime,
            forwardDir,
            @event.ImpactEffect,
            @event.ProjectilePrefab,
            spawnPos,
            spawnRot
        );
    }

    private void ExecuteAreaAttack(AttackExecutionRequestEvent @event, Transform attackerTf) { }

    private void ExecuteBeamAttack(AttackExecutionRequestEvent @event, Transform attackerTf) { }

    private void ExecuteCustomAttack(AttackExecutionRequestEvent @event, Transform attackerTf) { }

    // Clears damage cache when a NEW attack starts
    // This is critical because ATTACK_END may not fire reliably during animator swaps (e.g., mech transformation)
    private void OnAttackStart(AttackPressedInputEvent @event)
    {
        if (@event.Entity.Equals(EntityInstance))
        {
            // Clear cache for this attacker when starting a new attack
            if (_attackHitCache.ContainsKey(@event.Entity))
            {
                _attackHitCache[@event.Entity].Clear();
            }
        }
    }

    // Also clears damage cache when attack animation ends (backup cleanup)
    private void OnAnimationRelay(AnimationEventRelayEvent @event)
    {
        if (@event.EventType == AnimationEventRelayType.ATTACK_END)
        {
            _attackHitCache.Remove(@event.Entity);
        }
    }

    private void OnEntityDeath(EntityDeathEvent @event)
    {
        if (@event.Entity.Equals(EntityInstance))
        {
            ClearDamageCache();
        }
    }

    private void OnPlayerRespawned(PlayerRespawnedEvent @event)
    {
        if (@event.Entity.Equals(EntityInstance))
        {
            ClearDamageCache();
        }
    }

    /// <summary>
    /// When any entity is destroyed, remove its ID from all target HashSets in the cache.
    /// This prevents stale IDs from blocking damage when entity IDs are recycled.
    /// </summary>
    private void OnEntityDestroyed(EntityDestroyedEvent @event)
    {
        // Remove the destroyed entity from all target HashSets
        foreach (var kvp in _attackHitCache)
        {
            kvp.Value.Remove(@event.Entity);
        }
    }

    /// <summary>
    /// Manually clears the attack damage cache.
    /// Used when attacks are interrupted (e.g. by mech transformation) and ATTACK_END event is missed.
    /// </summary>
    public void ClearDamageCache()
    {
        _attackHitCache.Clear();
    }

    private void TriggerAimingRigForAttack(AttackExecutionRequestEvent @event, Transform attackerTf)
    {
        if (!_registry.TryGet(@event.Attacker, out EntityView attackerView))
        {
            return;
        }

        AimingRigView aimingRig = attackerView.GetComponent<AimingRigView>();
        if (aimingRig == null)
        {
            return;
        }

        // Check if character has aiming rig enabled
        if (_world.Components.TryGet(@event.Attacker, out CharacterSelectionComponent characterSelection))
        {
            if (characterSelection.CharacterData != null && characterSelection.CharacterData.useAimingRig)
            {
                Vector3 aimTarget = attackerTf.position + @event.Direction.normalized * @event.Range;
                if (@event.Direction.sqrMagnitude < 0.001f)
                {
                    aimTarget = attackerTf.position + attackerTf.forward * @event.Range;
                }

                // Aim for the attack duration (typically 0.5-1 second)
                aimingRig.StartAiming(aimTarget, 1f);
            }
        }
    }

    private void OnDestroy()
    {
        if (_isInitialized && _world != null)
        {
            _world.Events.Unsubscribe<AttackExecutionRequestEvent>(OnExecuteAttack);
            _world.Events.Unsubscribe<AnimationEventRelayEvent>(OnAnimationRelay);
            _world.Events.Unsubscribe<EntityDeathEvent>(OnEntityDeath);
            _world.Events.Unsubscribe<PlayerRespawnedEvent>(OnPlayerRespawned);
            _world.Events.Unsubscribe<EntityDestroyedEvent>(OnEntityDestroyed);
            _world.Events.Unsubscribe<AttackPressedInputEvent>(OnAttackStart);
        }
    }
}
