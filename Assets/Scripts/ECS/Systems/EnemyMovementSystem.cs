using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class EnemyMovementSystem : ISystem
{
    private World _world;
    private const float FOOTSTEP_INTERVAL = 0.4f; // Time between footsteps
    private readonly Dictionary<EntityId, float> _lastFootstepTime = new();

    // --- Config ---
    private const float WAYPOINT_TOLERANCE = 0.5f;
    private const float NO_PROGRESS_THRESHOLD = 0.1f;
    private const float NO_PROGRESS_REPATH_TIME = 0.5f;  // Reduced from 0.8f - faster unstuck
    private const float STUCK_REPATH_TIME = 0.4f;        // Reduced from 0.6f - faster unstuck
    private const float DEFAULT_REPATH_INTERVAL = 0.6f;  // Reduced from 0.8f - more frequent updates
    private const float RECALC_DISTANCE_THRESHOLD = 0.5f;
    private const float STUCK_NUDGE_STRENGTH = 0.4f;     // Increased from 0.3f - stronger push

    // --- GRAVITY ---
    private const float GRAVITY = -9.81f;
    private const float GROUND_CHECK_DISTANCE = 0.2f;

    // Increased rotation speed for faster turning
    private readonly float rotateSpeed = 180f;  // Increased from 50f
    private readonly Collider[] _overlapBuffer = new Collider[16];

    public void Initialize(World world) => _world = world;

    public void FixedUpdate(float dt) { }

    public void Shutdown()
    {
        _lastFootstepTime.Clear();
    }

    public void Update(float dt)
    {
        if (!NetworkManager.Singleton.IsServer)
        {
            return;
        }

        foreach (
            var (entity, enemy, trans, movement) in _world.Components.Query<
                EnemyComponent,
                TransformComponent,
                MovementDataComponent
            >()
        )
        {
            // Apply gravity first
            ApplyGravity(entity, trans, movement, dt);

            if (
                enemy.CurrentState == EnemyState.Idle
                || enemy.CurrentState == EnemyState.Attack
                || enemy.CurrentState == EnemyState.Dead
            )
            {
                continue;
            }

            if (!enemy.HasPath)
            {
                ResetProgress(enemy, trans);
                continue;
            }

            if (ReachedFinalDestination(entity, enemy, trans))
                continue;

            MoveTowardsWaypoint(entity, enemy, trans, movement, dt);
            CheckProgress(entity, enemy, trans, dt);
            CheckPeriodicRepath(entity, enemy, trans);
        }
    }

    private void ApplyGravity(EntityId entity, TransformComponent trans, MovementDataComponent movement, float dt)
    {
        var registry = _world.Services.Resolve<EntityViewRegistry>();
        if (!registry.TryGet(entity, out EntityView view))
        {
            return;
        }

        // Ground check using raycast
        LayerMask groundMask = LayerMask.GetMask("Default", "Ground");
        bool wasGrounded = movement.IsGrounded;

        movement.IsGrounded = Physics.Raycast(
            trans.Position + Vector3.up * 0.1f,
            Vector3.down,
            GROUND_CHECK_DISTANCE + 0.1f,
            groundMask,
            QueryTriggerInteraction.Ignore
        );

        if (movement.IsGrounded)
        {
            // On ground - reset vertical velocity and snap to ground
            movement.VerticalVelocity = 0f;

            // Raycast to find exact ground position
            if (
                Physics.Raycast(
                    trans.Position + Vector3.up * 0.5f,
                    Vector3.down,
                    out RaycastHit hit,
                    1f,
                    groundMask,
                    QueryTriggerInteraction.Ignore
                )
            )
            {
                trans.Position = new Vector3(trans.Position.x, hit.point.y, trans.Position.z);
            }
        }
        else
        {
            // In air - apply gravity
            movement.VerticalVelocity += GRAVITY * dt;
            trans.Position += Vector3.up * movement.VerticalVelocity * dt;
        }

        // Update GameObject transform
        view.transform.position = trans.Position;
    }

    private void ResetProgress(EnemyComponent path, TransformComponent trans)
    {
        path.NoProgressTimer = 0f;
        path.StuckTimer = 0f;
        path.LastAgentPosition = trans.Position;
    }

    private bool ReachedFinalDestination(EntityId entity, EnemyComponent enemy, TransformComponent trans)
    {
        if (enemy.WaypointIndex < enemy.Path.Count)
        {
            return false;
        }

        float distToGoal = Vector3.Distance(trans.Position, enemy.Path[^1]);

        if (distToGoal > enemy.StoppingDistance)
        {
            return false;
        }

        if (enemy.CurrentState == EnemyState.Chase || enemy.CurrentState == EnemyState.Patrol)
        {
            if (Vector3.Distance(trans.Position, enemy.LastRequestedTarget) > enemy.StoppingDistance + 0.2f)
            {
                RequestRepath(entity, enemy);
            }
            else
            {
                enemy.Path.Clear();
                enemy.WaypointIndex = enemy.Path.Count;
            }
        }
        else
        {
            enemy.Path.Clear();
            enemy.WaypointIndex = enemy.Path.Count;
        }
        return true;
    }

    private void MoveTowardsWaypoint(
        EntityId entity,
        EnemyComponent enemy,
        TransformComponent trans,
        MovementDataComponent movement,
        float dt
    )
    {
        if (enemy.Path == null || enemy.Path.Count == 0)
        {
            movement.IsMoving = false;
            return;
        }

        // Guard against overflow
        if (enemy.WaypointIndex >= enemy.Path.Count)
        {
            enemy.WaypointIndex = enemy.Path.Count - 1;
        }

        Vector3 targetPos = enemy.Path[enemy.WaypointIndex];

        //  --- Check Obstacle ahead ----
        Vector3 forward = targetPos - trans.Position;
        forward.y = 0; // Flatten for horizontal movement

        float forwardDist = Mathf.Min(1.0f, forward.magnitude);
        Vector3 forwardDir = forward.normalized;

        if (forwardDist > 0.05f)
        {
            float castRadius = 0.25f;
            LayerMask mask = GridSystem.Instance.GetObstacleLayer();

            if (
                Physics.SphereCast(
                    trans.Position + Vector3.up * 0.5f,
                    castRadius,
                    forwardDir,
                    out RaycastHit hit,
                    forwardDist + 0.05f,
                    mask,
                    QueryTriggerInteraction.Ignore
                )
            )
            {
                // Found obstacle - nudge away and request new path
                Vector3 nudge = hit.normal * 0.2f;
                trans.Position += nudge;
                RequestRepath(entity, enemy);
                return;
            }
        }

        // --- Avoid multiple enemy crowding ----
        // OPTIMIZATION: Only check separation every 5 frames to reduce physics overhead
        if ((Time.frameCount + entity.Id) % 5 == 0)
        {
            Vector3 separation = Vector3.zero;
            float checkRadius = 0.6f;

            int cnt = Physics.OverlapSphereNonAlloc(trans.Position, checkRadius, _overlapBuffer);

            for (int i = 0; i < cnt; i++)
            {
                if (!_overlapBuffer[i].TryGetComponent(out EntityView ev))
                {
                    continue;
                }

                if (ev.EntityInstance.Equals(entity))
                {
                    continue;
                }

                if (!_world.Components.Has<EnemyComponent>(ev.EntityInstance))
                {
                    continue;
                }

                Vector3 dirAway = trans.Position - ev.transform.position;
                dirAway.y = 0;

                float sqr = dirAway.sqrMagnitude;
                if (sqr < 0.0001f)
                {
                    continue;
                }

                separation += dirAway.normalized / Mathf.Sqrt(sqr);
            }

            if (separation.sqrMagnitude > 0.0001f)
            {
                // Multiply by 5 to compensate for checking every 5th frame
                Vector3 sepMove = separation.normalized * movement.MoveSpeed * 0.5f * dt * 5f;
                trans.Position += new Vector3(sepMove.x, 0, sepMove.z);
            }
        }

        // Move towards waypoint (horizontal only)
        Vector3 currentPosFlat = new Vector3(trans.Position.x, targetPos.y, trans.Position.z);
        Vector3 targetPosFlat = new Vector3(targetPos.x, targetPos.y, targetPos.z);

        Vector3 newPos = Vector3.MoveTowards(currentPosFlat, targetPosFlat, movement.MoveSpeed * dt);

        // Keep Y from gravity system
        trans.Position = new Vector3(newPos.x, trans.Position.y, newPos.z);

        Vector3 dir = targetPosFlat - currentPosFlat;

        movement.IsMoving = dir.sqrMagnitude > 0.0001f;
        movement.MoveDirection = dir.normalized;

        // Play footstep sound when moving and grounded
        if (movement.IsMoving && movement.IsGrounded)
        {
            float currentTime = Time.time;
            if (
                !_lastFootstepTime.TryGetValue(entity, out float lastTime)
                || currentTime - lastTime >= FOOTSTEP_INTERVAL
            )
            {
                _lastFootstepTime[entity] = currentTime;
                _world.Events.Publish(new AudioCueEvent(entity, SoundType.Footstep, trans.Position));
            }
        }
        else
        {
            // Stop footstep sound when not moving
            var audioService = _world.Services.Resolve<IAudioService>();
            audioService?.StopFootstepForEntity(entity);
        }

        if (dir.sqrMagnitude > 0.0001f)
        {
            trans.Rotation = Quaternion.Slerp(
                trans.Rotation,
                Quaternion.LookRotation(dir.normalized, Vector3.up),
                dt * rotateSpeed
            );
        }

        // Sync ECS transform to Unity Transform
        // Without this, enemies animate but don't actually move visually!
        var registry = _world.Services.Resolve<EntityViewRegistry>();
        if (registry.TryGet(entity, out EntityView view))
        {
            view.transform.position = trans.Position;
            view.transform.rotation = trans.Rotation;
        }

        // Check if reached waypoint (horizontal distance only)
        float distToWaypoint = Vector3.Distance(
            new Vector3(trans.Position.x, 0, trans.Position.z),
            new Vector3(targetPos.x, 0, targetPos.z)
        );

        if (distToWaypoint <= WAYPOINT_TOLERANCE)
        {
            enemy.WaypointIndex++;
            enemy.StuckTimer = 0f;
        }
        else
        {
            enemy.StuckTimer += dt;
            if (enemy.StuckTimer > STUCK_REPATH_TIME)
            {
                RequestRepath(entity, enemy);
                enemy.StuckTimer = 0f;
            }
        }
    }

    private void CheckProgress(EntityId entity, EnemyComponent enemy, TransformComponent trans, float dt)
    {
        float moved = Vector3.Distance(trans.Position, enemy.LastAgentPosition);

        if (moved < NO_PROGRESS_THRESHOLD)
        {
            enemy.NoProgressTimer += dt;
            if (enemy.NoProgressTimer > NO_PROGRESS_REPATH_TIME)
            {
                // Apply random nudge to escape corners/stuck positions
                ApplyStuckNudge(entity, enemy, trans);
                
                RequestRepath(entity, enemy);
                enemy.NoProgressTimer = 0f;
                
                // Boss: Track consecutive stuck occurrences
                if (enemy.IsBoss && _world.Components.TryGet(entity, out BossComponent boss))
                {
                    boss.ConsecutiveStuckChecks++;
                    
                    // If boss is stuck 3+ times in a row, teleport closer to target
                    if (boss.ConsecutiveStuckChecks >= 3 && !enemy.TargetEntity.Equals(default))
                    {
                        TeleportBossNearTarget(entity, enemy, boss, trans);
                        boss.ConsecutiveStuckChecks = 0;
                    }
                }
            }
        }
        else
        {
            enemy.NoProgressTimer = 0f;
            
            // Reset stuck counter when making progress
            if (enemy.IsBoss && _world.Components.TryGet(entity, out BossComponent boss))
            {
                boss.ConsecutiveStuckChecks = 0;
            }
        }

        enemy.LastAgentPosition = trans.Position;
    }
    
    /// <summary>
    /// Teleports boss near its target when stuck too long (last resort)
    /// </summary>
    private void TeleportBossNearTarget(EntityId entity, EnemyComponent enemy, BossComponent boss, TransformComponent trans)
    {
        var registry = _world.Services.Resolve<EntityViewRegistry>();
        
        if (!registry.TryGet(enemy.TargetEntity, out EntityView targetView))
        {
            return;
        }
        
        Vector3 targetPos = targetView.transform.position;
        Vector3 dirToTarget = (targetPos - trans.Position).normalized;
        
        // Teleport 5 units away from target, in the direction the boss was trying to go
        Vector3 teleportPos = targetPos - dirToTarget * 5f;
        teleportPos.y = trans.Position.y; // Keep same Y
        
        trans.Position = teleportPos;
        
        // Update view
        if (registry.TryGet(entity, out EntityView bossView))
        {
            bossView.transform.position = teleportPos;
        }
        
        // Request new path immediately
        RequestRepath(entity, enemy);
        
        Debug.Log($"[EnemyMovementSystem] Boss teleported to target area after being stuck");
    }

    private void CheckPeriodicRepath(EntityId entity, EnemyComponent enemy, TransformComponent trans)
    {
        if (Time.time - enemy.LastRequestTime <= DEFAULT_REPATH_INTERVAL)
        {
            return;
        }

        if (enemy.LastRequestedTarget == Vector3.positiveInfinity)
        {
            return;
        }

        float dist = Vector3.Distance(trans.Position, enemy.LastRequestedTarget);
        if (dist > RECALC_DISTANCE_THRESHOLD)
        {
            RequestRepath(entity, enemy);
        }
    }

    private void RequestRepath(EntityId entity, EnemyComponent enemy)
    {
        if (
            enemy.CurrentState != EnemyState.Chase
            && enemy.CurrentState != EnemyState.Patrol
            && enemy.CurrentState != EnemyState.TakeCover
        )
        {
            return;
        }

        _world.Events.Publish(new EnemyPathRequestEvent(entity, enemy.LastRequestedTarget, enemy.StoppingDistance));
        enemy.LastRequestTime = Time.time;
    }

    /// <summary>
    /// Applies a random nudge to push enemy out of stuck positions (corners, obstacles)
    /// </summary>
    private void ApplyStuckNudge(EntityId entity, EnemyComponent enemy, TransformComponent trans)
    {
        var registry = _world.Services.Resolve<EntityViewRegistry>();
        if (!registry.TryGet(entity, out EntityView view))
        {
            return;
        }

        // Generate random horizontal direction
        float randomAngle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        Vector3 nudgeDir = new Vector3(Mathf.Cos(randomAngle), 0f, Mathf.Sin(randomAngle));

        // Try to nudge away from obstacles using raycast
        LayerMask obstacleMask = GridSystem.Instance?.GetObstacleLayer() ?? LayerMask.GetMask("Default");
        
        // Cast rays in multiple directions to find clear path
        Vector3 bestDir = nudgeDir;
        float bestDist = 0f;
        
        for (int i = 0; i < 8; i++)
        {
            float angle = i * 45f * Mathf.Deg2Rad;
            Vector3 testDir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            
            if (!Physics.Raycast(
                trans.Position + Vector3.up * 0.5f,
                testDir,
                out RaycastHit hit,
                2f,
                obstacleMask,
                QueryTriggerInteraction.Ignore
            ))
            {
                // No obstacle in this direction - it's the best!
                bestDir = testDir;
                bestDist = 2f;
                break;
            }
            else if (hit.distance > bestDist)
            {
                bestDir = testDir;
                bestDist = hit.distance;
            }
        }

        // Apply nudge in best direction
        Vector3 nudge = bestDir * STUCK_NUDGE_STRENGTH;
        trans.Position += nudge;
        view.transform.position = trans.Position;
    }
}
