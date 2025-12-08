using Unity.Netcode;
using UnityEngine;

public class EnemyMovementSystem : ISystem
{
    private World _world;

    // --- Config ---
    private const float WAYPOINT_TOLERANCE = 0.5f;
    private const float NO_PROGRESS_THRESHOLD = 0.1f;
    private const float NO_PROGRESS_REPATH_TIME = 2f;
    private const float STUCK_REPATH_TIME = 1.5f;
    private const float DEFAULT_REPATH_INTERVAL = 1f;
    private const float RECALC_DISTANCE_THRESHOLD = 0.5f;

    // --- GRAVITY ---
    private const float GRAVITY = -9.81f;
    private const float GROUND_CHECK_DISTANCE = 0.2f;

    private readonly float rotateSpeed = 50f;
    private readonly Collider[] _overlapBuffer = new Collider[16];

    public void Initialize(World world) => _world = world;

    public void FixedUpdate(float dt) { }

    public void Shutdown() { }

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
            Vector3 sepMove = separation.normalized * movement.MoveSpeed * 0.5f * dt;
            trans.Position += new Vector3(sepMove.x, 0, sepMove.z); // Only horizontal separation
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

        if (dir.sqrMagnitude > 0.0001f)
        {
            trans.Rotation = Quaternion.Slerp(
                trans.Rotation,
                Quaternion.LookRotation(dir.normalized, Vector3.up),
                dt * rotateSpeed
            );
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
                RequestRepath(entity, enemy);
                enemy.NoProgressTimer = 0f;
            }
        }
        else
        {
            enemy.NoProgressTimer = 0f;
        }

        enemy.LastAgentPosition = trans.Position;
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
}
