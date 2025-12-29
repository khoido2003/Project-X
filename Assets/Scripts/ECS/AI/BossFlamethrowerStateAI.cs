using UnityEngine;

public class BossFlamethrowerStateAI : IEnemyState
{
    public EnemyState StateType => EnemyState.Flamethrower;

    private float _tickTimer;
    private float _totalTime;

    public void OnEnter(World world, EntityId entity)
    {
        var boss = world.Components.Get<BossComponent>(entity);
        var enemyTf = world.Components.Get<TransformComponent>(entity);

        boss.IsFlaming = true;
        boss.FlameProgress = 0f;
        boss.LastFlamethrowerTime = Time.time;
        _tickTimer = 0f;
        _totalTime = 0f;

        // Trigger flamethrower animation
        world.Events.Publish(new AnimationParameterEvent(entity, "flamethrower", AnimationParameterType.Trigger, null));

        // Spawn flamethrower VFX attached to boss (server side)
        var registry = world.Services.Resolve<EntityViewRegistry>();
        if (boss.FlamethrowerVFXPrefab != null)
        {
            if (registry.TryGet(entity, out EntityView view))
            {
                boss.ActiveFlameVFX = Object.Instantiate(boss.FlamethrowerVFXPrefab, view.transform);
                boss.ActiveFlameVFX.transform.localPosition = new Vector3(0, 1.5f, 0.5f); // Position in front
                boss.ActiveFlameVFX.Play();
            }
        }
        
        // Broadcast VFX start to all clients
        if (registry.TryGet(entity, out EntityView entityView) && entityView.TryGetComponent(out EnemyNetworkSyncView syncView))
        {
            syncView.BroadcastBossFlamethrowerVfxClientRpc(true);
        }

        // Play flamethrower sound directly using the clip from BossComponent
        if (boss.FlamethrowerSound != null)
        {
            AudioHelper.PlaySound3D(world, boss.FlamethrowerSound, AudioCategory.Enemy, enemyTf.Position);
        }
    }

    public void OnUpdate(World world, EntityId entity, float dt)
    {
        var enemy = world.Components.Get<EnemyComponent>(entity);
        var boss = world.Components.Get<BossComponent>(entity);
        var enemyTf = world.Components.Get<TransformComponent>(entity);

        _totalTime += dt;
        _tickTimer += dt;
        boss.FlameProgress = _totalTime / boss.FlamethrowerDuration;

        // Slowly rotate to sweep the flame
        if (world.Components.TryGet(enemy.TargetEntity, out TransformComponent targetTf))
        {
            FaceTargetSlowly(world, entity, enemyTf, targetTf, dt);
        }

        // Deal damage on tick interval
        if (_tickTimer >= boss.FlamethrowerTickInterval)
        {
            _tickTimer = 0f;
            DealFlameDamage(world, entity, boss, enemyTf);
        }

        // Check if flamethrower duration ended
        if (_totalTime >= boss.FlamethrowerDuration)
        {
            boss.IsFlaming = false;
            TransitionAfterFlame(world, entity, enemy);
        }
    }

    private void FaceTargetSlowly(
        World world,
        EntityId entity,
        TransformComponent enemyTf,
        TransformComponent targetTf,
        float dt
    )
    {
        Vector3 dir = targetTf.Position - enemyTf.Position;
        dir.y = 0;

        if (dir.sqrMagnitude < 0.01f)
        {
            return;
        }

        Quaternion targetRot = Quaternion.LookRotation(dir.normalized);

        // Slow rotation speed for sweeping effect (60 degrees per second)
        float rotateSpeed = 60f;
        enemyTf.Rotation = Quaternion.RotateTowards(enemyTf.Rotation, targetRot, rotateSpeed * dt);

        // Update view
        var registry = world.Services.Resolve<EntityViewRegistry>();
        if (registry.TryGet(entity, out EntityView view))
        {
            view.transform.rotation = enemyTf.Rotation;
        }
    }

    private void DealFlameDamage(World world, EntityId entity, BossComponent boss, TransformComponent enemyTf)
    {
        // Find all players within cone
        foreach (
            var (playerEntity, player, playerTf, health) in world.Components.Query<
                PlayerTagComponent,
                TransformComponent,
                HealthDataComponent
            >()
        )
        {
            // Skip untargetable or dead players
            if (health.IsUntargetable || health.IsDead) continue;
            
            Vector3 toPlayer = playerTf.Position - enemyTf.Position;
            float dist = toPlayer.magnitude;

            // Check range
            if (dist > boss.FlamethrowerRange)
            {
                continue;
            }

            // Check cone angle
            toPlayer.y = 0;
            Vector3 forward = enemyTf.Rotation * Vector3.forward;
            float angle = Vector3.Angle(forward, toPlayer);

            if (angle <= boss.FlamethrowerAngle)
            {
                // Player is in the flame cone - deal damage
                world.Events.Publish(new DamageEvent(playerEntity, entity, boss.FlamethrowerDamagePerTick));
            }
        }
    }

    private void TransitionAfterFlame(World world, EntityId entity, EnemyComponent enemy)
    {
        if (enemy.TargetEntity.Equals(default))
        {
            EnemyAIHelpers.ChangeState(world, entity, EnemyState.Patrol);
            return;
        }

        // Check distance to target
        if (world.Components.TryGet(enemy.TargetEntity, out TransformComponent targetTf))
        {
            var enemyTf = world.Components.Get<TransformComponent>(entity);
            var weapon = world.Components.Get<WeaponDataComponent>(entity);

            float dist = Vector3.Distance(enemyTf.Position, targetTf.Position);

            if (dist <= weapon.BaseRange)
            {
                EnemyAIHelpers.ChangeState(world, entity, EnemyState.Attack);
            }
            else
            {
                EnemyAIHelpers.ChangeState(world, entity, EnemyState.Chase);
            }
        }
        else
        {
            enemy.TargetEntity = default;
            EnemyAIHelpers.ChangeState(world, entity, EnemyState.Patrol);
        }
    }

    public void OnExit(World world, EntityId entity)
    {
        if (world.Components.TryGet(entity, out BossComponent boss))
        {
            boss.IsFlaming = false;
            boss.FlameProgress = 0f;
            
            // Cleanup flamethrower VFX on server
            if (boss.ActiveFlameVFX != null)
            {
                boss.ActiveFlameVFX.Stop();
                Object.Destroy(boss.ActiveFlameVFX.gameObject, 0.5f);
                boss.ActiveFlameVFX = null;
            }
            
            // Broadcast VFX stop to all clients
            var registry = world.Services.Resolve<EntityViewRegistry>();
            if (registry.TryGet(entity, out EntityView view) && view.TryGetComponent(out EnemyNetworkSyncView syncView))
            {
                syncView.BroadcastBossFlamethrowerVfxClientRpc(false);
            }
        }
    }
}
