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

    private void ExecuteMeleeAttack(AttackExecutionRequestEvent @event, Transform attackerTf)
    {
        // Ensure cache exists for this attacker
        if (!_attackHitCache.TryGetValue(@event.Attacker, out var damagedEntities))
        {
            damagedEntities = new HashSet<EntityId>();
            _attackHitCache[@event.Attacker] = damagedEntities;
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

            if (@event.ImpactEffect)
            {
                Instantiate(@event.ImpactEffect, hit.ClosestPoint(origin), Quaternion.identity);
            }
        }
    }

    private void ExecuteProjectileAttack(AttackExecutionRequestEvent @event, Transform attackerTf)
    {
        if (@event.ProjectilePrefab == null)
        {
            Debug.LogError($"No projectile prefab found!");
            return;
        }

        Vector3 spawnPos =
            transform.position + new Vector3(0f, 1.3f, 0f) + attackerTf.TransformDirection(@event.SpawnOffset);

        Quaternion spawnRot = Quaternion.LookRotation(
            (@event.Direction.sqrMagnitude < 0.0001f) ? attackerTf.forward : @event.Direction.normalized,
            Vector3.up
        );

        var pool = _world.Services.Resolve<ObjectPoolService>();

        GameObject projectileGO = pool.Get(@event.ProjectilePrefab, spawnPos, attackerTf.rotation);

        if (!projectileGO.TryGetComponent(out ProjectileView projectile))
        {
            projectile = projectileGO.AddComponent<ProjectileView>();
        }

        Vector3 forwardDir = @event.Direction.sqrMagnitude < 0.0001f ? attackerTf.forward : @event.Direction.normalized;

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

    // Clears damage cache when attack animation ends
    private void OnAnimationRelay(AnimationEventRelayEvent @event)
    {
        if (@event.EventType == AnimationEventRelayType.ATTACK_END)
        {
            _attackHitCache.Remove(@event.Entity);
        }
    }

    private void OnDestroy()
    {
        if (_isInitialized && _world != null)
        {
            _world.Events.Unsubscribe<AttackExecutionRequestEvent>(OnExecuteAttack);
            _world.Events.Unsubscribe<AnimationEventRelayEvent>(OnAnimationRelay);
        }
    }
}
