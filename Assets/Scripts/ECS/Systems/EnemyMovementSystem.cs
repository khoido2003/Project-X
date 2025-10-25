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

    public void Initialize(World world) => _world = world;

    public void FixedUpdate(float dt) { }

    public void Shutdown() { }

    public void Update(float dt)
    {
        foreach (var (entity, enemy, trans) in _world.Components.Query<EnemyComponent, TransformComponent>())
        {
            if (!enemy.HasPath)
            {
                ResetProgress(enemy, trans);
                continue;
            }

            if (ReachedFinalDestination(entity, enemy, trans))
                continue;

            MoveTowardsWaypoint(entity, enemy, trans, dt);
            CheckProgress(entity, enemy, trans, dt);
            CheckPeriodicRepath(entity, enemy, trans);
            HandleAnimation(entity, enemy);
        }
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
            return false;

        float distToGoal = Vector3.Distance(trans.Position, enemy.Path[^1]);

        if (distToGoal > enemy.StoppingDistance)
            return false;

        if (enemy.CurrentState == EnemyState.Chase || enemy.CurrentState == EnemyState.Patrol)
        {
            if (Vector3.Distance(trans.Position, enemy.LastRequestedTarget) > enemy.StoppingDistance + 0.2f)
            {
                RequestRepath(entity, enemy);
            }
            else
            {
                enemy.Path.Clear();
                enemy.WaypointIndex = enemy.Path.Count; // mark as finished but not null
            }
        }
        else
        {
            enemy.Path.Clear();
            enemy.WaypointIndex = enemy.Path.Count; // mark as finished but not null
        }
        return true;
    }

    private void MoveTowardsWaypoint(EntityId entity, EnemyComponent enemy, TransformComponent trans, float dt)
    {
        if (enemy.Path == null || enemy.Path.Count == 0)
            return;

        // Guard against overflow
        if (enemy.WaypointIndex >= enemy.Path.Count)
            enemy.WaypointIndex = enemy.Path.Count - 1;

        Vector3 targetPos = enemy.Path[enemy.WaypointIndex];
        Vector3 newPos = Vector3.MoveTowards(trans.Position, targetPos, enemy.MoveSpeed * dt);

        trans.Position = newPos;

        Vector3 dir = targetPos - newPos;
        if (dir.sqrMagnitude > 0.0001f)
        {
            trans.Rotation = Quaternion.Slerp(
                trans.Rotation,
                Quaternion.LookRotation(dir.normalized, Vector3.up),
                dt * 10f
            );
        }

        if (Vector3.Distance(newPos, targetPos) <= WAYPOINT_TOLERANCE)
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
            return;

        if (enemy.LastRequestedTarget == Vector3.positiveInfinity)
            return;

        float dist = Vector3.Distance(trans.Position, enemy.LastRequestedTarget);
        if (dist > RECALC_DISTANCE_THRESHOLD)
        {
            RequestRepath(entity, enemy);
        }
    }

    private void RequestRepath(EntityId entity, EnemyComponent enemy)
    {
        _world.Events.Publish(new EnemyPathRequestEvent(entity, enemy.LastRequestedTarget, enemy.StoppingDistance));
        enemy.LastRequestTime = Time.time;
    }

    private void HandleAnimation(EntityId entity, EnemyComponent enemy)
    {
        if (_world.Components.TryGet(entity, out AnimationDataComponent anim))
        {
            bool isMoving = enemy.HasPath && enemy.WaypointIndex < enemy.Path.Count;
            if (anim.IsMoving != isMoving)
            {
                anim.IsMoving = isMoving;
                _world.Events.Publish(
                    new AnimationParameterEvent(entity, anim.IsMovingParam, AnimationParameterType.Bool, isMoving)
                );
            }
        }
    }
}
