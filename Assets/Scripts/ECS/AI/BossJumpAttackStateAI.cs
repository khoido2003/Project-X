using UnityEngine;

/// <summary>
/// Boss-only AI state for executing a jump attack to close distance with ranged players.
/// The boss leaps to the target position and deals AoE damage on landing.
/// Uses BossComponent for boss-specific fields.
/// </summary>
public class BossJumpAttackStateAI : IEnemyState
{
    public EnemyState StateType => EnemyState.JumpAttack;

    private const float JUMP_HEIGHT = 4f; // Peak height of jump arc
    private Vector3 _startPosition; // Store start position for arc calculation

    public void OnEnter(World world, EntityId entity)
    {
        var enemy = world.Components.Get<EnemyComponent>(entity);
        var boss = world.Components.Get<BossComponent>(entity);
        var enemyTf = world.Components.Get<TransformComponent>(entity);

        boss.IsJumping = true;
        boss.JumpProgress = 0f;
        boss.LastJumpTime = Time.time;
        _startPosition = enemyTf.Position;

        // Calculate jump target position (predict where player will be)
        if (world.Components.TryGet(enemy.TargetEntity, out TransformComponent targetTf))
        {
            Vector3 targetPos = targetTf.Position;

            // Add slight prediction based on target velocity
            if (world.Components.TryGet(enemy.TargetEntity, out MovementDataComponent targetMove))
            {
                targetPos += targetMove.Velocity * (boss.JumpDuration * 0.5f);
            }

            boss.JumpTargetPosition = targetPos;
        }
        else
        {
            // No valid target, jump forward
            boss.JumpTargetPosition = enemyTf.Position + enemyTf.Rotation * Vector3.forward * 5f;
        }

        // Trigger jump animation
        world.Events.Publish(new AnimationParameterEvent(entity, "jumpAttack", AnimationParameterType.Trigger, null));

        // Play jump sound directly using the clip from BossComponent
        if (boss.JumpSound != null)
        {
            AudioHelper.PlaySound3D(world, boss.JumpSound, AudioCategory.Enemy, enemyTf.Position);
        }
        
        // Broadcast audio to clients
        var registry = world.Services.Resolve<EntityViewRegistry>();
        if (registry.TryGet(entity, out EntityView view) && view.TryGetComponent(out EnemyNetworkSyncView syncView))
        {
            syncView.BroadcastBossAudioClientRpc(0, enemyTf.Position); // 0 = Jump sound
        }
    }

    public void OnUpdate(World world, EntityId entity, float dt)
    {
        var enemy = world.Components.Get<EnemyComponent>(entity);
        var boss = world.Components.Get<BossComponent>(entity);
        var enemyTf = world.Components.Get<TransformComponent>(entity);

        if (!boss.IsJumping)
        {
            // Jump completed, transition to attack or chase
            TransitionAfterLanding(world, entity, enemy, boss);
            return;
        }

        // Progress the jump
        boss.JumpProgress += dt / boss.JumpDuration;

        if (boss.JumpProgress >= 1f)
        {
            // Landing!
            boss.JumpProgress = 1f;
            boss.IsJumping = false;

            // CRITICAL: Validate landing position has ground beneath it
            // Use raycast to find actual ground level, preventing falling through terrain
            Vector3 landingPos = boss.JumpTargetPosition;
            Vector3 rayOrigin = new Vector3(landingPos.x, landingPos.y + 5f, landingPos.z);
            
            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 20f, LayerMask.GetMask("Ground", "Default")))
            {
                landingPos = hit.point;
            }
            else
            {
                // Fallback: If no ground found, use start Y position (safe)
                Debug.LogWarning($"[BossJumpAttack] No ground found at landing position! Using start Y.");
                landingPos.y = _startPosition.y;
            }
            
            // Also try to sample NavMesh for valid position
            if (UnityEngine.AI.NavMesh.SamplePosition(landingPos, out UnityEngine.AI.NavMeshHit navHit, 3f, UnityEngine.AI.NavMesh.AllAreas))
            {
                landingPos = navHit.position;
            }

            // Set validated landing position
            enemyTf.Position = landingPos;

            // Update view position
            var registry = world.Services.Resolve<EntityViewRegistry>();
            if (registry.TryGet(entity, out EntityView view))
            {
                view.transform.position = landingPos;
                
                // Re-enable NavMeshAgent if it exists to snap to navmesh
                var agent = view.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (agent != null && !agent.enabled)
                {
                    agent.enabled = true;
                    agent.Warp(landingPos);
                }
            }

            // Deal AoE damage on landing
            DealLandingDamage(world, entity, boss, enemyTf);

            // Show landing VFX
            SpawnLandingVFX(world, entity, enemyTf);

            // Transition to next state
            TransitionAfterLanding(world, entity, enemy, boss);
        }
        else
        {
            // Interpolate position along jump arc
            float t = boss.JumpProgress;
            Vector3 flatPos = Vector3.Lerp(_startPosition, boss.JumpTargetPosition, t);

            // Vertical arc (parabola)
            float heightT = 1f - Mathf.Pow(2f * t - 1f, 2f); // Peaks at t=0.5
            float currentHeight = _startPosition.y + heightT * JUMP_HEIGHT;

            Vector3 newPos = new Vector3(flatPos.x, currentHeight, flatPos.z);

            // Update transform
            enemyTf.Position = newPos;

            // Update view
            var registry = world.Services.Resolve<EntityViewRegistry>();
            if (registry.TryGet(entity, out EntityView view))
            {
                view.transform.position = newPos;

                // Face toward landing target
                Vector3 lookDir = (boss.JumpTargetPosition - newPos);
                lookDir.y = 0;
                if (lookDir.sqrMagnitude > 0.01f)
                {
                    view.transform.rotation = Quaternion.LookRotation(lookDir.normalized);
                }
            }
        }
    }

    private void DealLandingDamage(World world, EntityId entity, BossComponent boss, TransformComponent enemyTf)
    {
        // Find all players within AoE radius
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
            
            float dist = Vector3.Distance(enemyTf.Position, playerTf.Position);

            if (dist <= boss.JumpAttackRadius)
            {
                // Deal damage
                world.Events.Publish(new DamageEvent(playerEntity, entity, boss.JumpAttackDamage));
            }
        }
    }

    private void SpawnLandingVFX(World world, EntityId entity, TransformComponent enemyTf)
    {
        var boss = world.Components.Get<BossComponent>(entity);
        
        // Spawn landing VFX on server
        if (boss.JumpLandingVFXPrefab != null)
        {
            var vfx = Object.Instantiate(boss.JumpLandingVFXPrefab, enemyTf.Position, Quaternion.identity);
            vfx.Play();
            Object.Destroy(vfx.gameObject, 3f); // Cleanup after 3 seconds
        }
        
        // Broadcast VFX to all clients
        var registry = world.Services.Resolve<EntityViewRegistry>();
        if (registry.TryGet(entity, out EntityView view) && view.TryGetComponent(out EnemyNetworkSyncView syncView))
        {
            syncView.BroadcastBossJumpLandingVfxClientRpc(enemyTf.Position);
            // Broadcast landing/impact audio to clients
            syncView.BroadcastBossAudioClientRpc(1, enemyTf.Position); // 1 = Landing sound
        }
        
        // Publish audio cue for the impact
        world.Events.Publish(new AudioCueEvent(entity, SoundType.Impact, enemyTf.Position));
    }

    private void TransitionAfterLanding(World world, EntityId entity, EnemyComponent enemy, BossComponent boss)
    {
        if (enemy.TargetEntity.Equals(default))
        {
            EnemyAIHelpers.ChangeState(world, entity, EnemyState.Patrol);
            return;
        }

        // Check if we landed close enough to attack
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
            boss.IsJumping = false;
            boss.JumpProgress = 0f;
        }
    }
}
