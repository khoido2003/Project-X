using System;
using System.Collections.Generic;
using UnityEngine;

public class PathfindingComponent : MonoBehaviour
{
    [Header("Pathfinding")]
    [SerializeField]
    private Transform target;

    [SerializeField]
    private float repathInterval = 1f;

    [SerializeField]
    private float recalcThreshold = 0.5f;

    [Header("Movement")]
    [SerializeField]
    private float waypointTolerance = 0.5f;

    [SerializeField]
    private float moveSpeed = 3f;

    [SerializeField]
    private float stoppingDistance = 0.5f;

    [Header("Debug mode")]
    [SerializeField]
    private bool enableDrawPath = true;

    // ------------------------------------

    private AStarPathfinder pathfinder = new();
    private List<Vector3> currentPath;
    private int pathIndex;

    private Vector3 lastPosition;
    private float noProgressTimer = 0f;
    private float repathTimer;
    private float stuckTimer = 0f;
    private Vector2Int lastTargetGrid;

    // ------------------------------------
    // EVENTS
    public event Action<List<Vector3>> OnPathCalculated;
    public event Action OnPathFailed;
    public event Action OnTargetReached;
    public event Action<int, Vector3> OnWaypointReached;

    // --------------------------------------
    // STATUS PROPERTIES
    public bool HasPath => currentPath != null && currentPath.Count > 0;
    public bool IsMoving => HasPath && pathIndex < currentPath.Count;
    public bool IsStuck => noProgressTimer > 2f || stuckTimer > 1.5f;
    public Vector3? CurrentWaypoint => IsMoving ? currentPath[pathIndex] : null;
    public float RemainingDistance
    {
        get
        {
            if (!IsMoving)
                return 0f;
            float dist = Vector3.Distance(transform.position, currentPath[pathIndex]);
            for (int i = pathIndex; i < currentPath.Count - 1; i++)
            {
                dist += Vector3.Distance(currentPath[i], currentPath[i + 1]);
            }
            return dist;
        }
    }

    //////////////////////////////////////////////////////////////////////

    private void Update()
    {
        TrackMovementProgress();
        UpdatePathToTarget();
        MoveAlongPath();
    }

    /// <summary>
    /// Move along the currently calculated path.
    /// </summary>
    private void MoveAlongPath()
    {
        if (currentPath == null || pathIndex >= currentPath.Count)
        {
            return;
        }

        Vector3 nextPos = currentPath[pathIndex];
        Vector3 moveDir = (nextPos - transform.position).normalized;

        // IF stoppingDistance exists then return soon here!
        if (pathIndex == currentPath.Count - 1)
        {
            float distToTarget = Vector3.Distance(transform.position, nextPos);
            if (distToTarget <= stoppingDistance)
            {
                OnTargetReached?.Invoke();
                currentPath = null;
                return;
            }
        }

        if (moveDir != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDir);

            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 10f);
        }

        // Make the agent slide away from corner instead of getting stuck
        float rayMaxDistance = 0.5f;
        if (
            Physics.Raycast(
                transform.position + Vector3.up * 0.5f,
                moveDir,
                out RaycastHit hit,
                rayMaxDistance,
                GridSystem.Instance.GetObstacleLayer(),
                QueryTriggerInteraction.Ignore
            )
        )
        {
            if (hit.collider.gameObject != gameObject)
            {
                Vector3 slideDirection = Vector3.Cross(Vector3.up, hit.normal).normalized;

                moveDir = (moveDir + slideDirection * 0.5f).normalized;
            }
        }

        // DON'T do this: this cause the object move like a robot
        // transform.position += moveDir * moveSpeed * Time.deltaTime;

        // Move toward the target
        transform.position = Vector3.MoveTowards(transform.position, nextPos, moveSpeed * Time.deltaTime);

        // Check if agent reached waypoint
        if (Vector3.Distance(transform.position, nextPos) < waypointTolerance)
        {
            OnWaypointReached?.Invoke(pathIndex, nextPos);

            pathIndex++;
            stuckTimer = 0f;

            if (pathIndex >= currentPath.Count)
            {
                OnTargetReached?.Invoke();
            }
        }
        else
        {
            stuckTimer += Time.deltaTime;

            // Stuck too long -> Find new path to the target
            if (stuckTimer > 1.5f)
            {
                ForceFindNewPath();
                stuckTimer = 0f;
            }
        }
    }

    /// <summary>
    /// Regularly updates the path toward the current target.
    /// </summary>
    private void UpdatePathToTarget()
    {
        if (target == null)
        {
            return;
        }

        // Recalculate path every X seconds so it follows moving target
        repathTimer += Time.deltaTime;

        if (repathTimer < repathInterval)
        {
            return;
        }

        GridSystem gridSystemInstance = GridSystem.Instance;

        Vector2Int startGrid = gridSystemInstance.GetGridPosition(transform.position);
        Vector2Int targetGrid = gridSystemInstance.GetGridPosition(target.position);

        GridLayer<bool> walkable = gridSystemInstance.GetLayer<bool>(GridLayerName.WALKABLE);

        // Check start and target grid are valid
        if (!walkable.GetValue(startGrid.x, startGrid.y))
        {
            startGrid = gridSystemInstance.FindNearestWalkable(startGrid);
        }

        if (!walkable.GetValue(targetGrid.x, targetGrid.y))
        {
            targetGrid = gridSystemInstance.FindNearestWalkable(targetGrid);
        }

        if (!gridSystemInstance.IsValidPosition(startGrid) || !gridSystemInstance.IsValidPosition(targetGrid))
        {
            return;
        }

        Vector3 snappedTarget = gridSystemInstance.GetWorldPosition(targetGrid);

        if (Vector3.Distance(snappedTarget, gridSystemInstance.GetWorldPosition(lastTargetGrid)) > recalcThreshold)
        {
            Vector3 snappedStart = gridSystemInstance.GetWorldPosition(startGrid);

            currentPath = pathfinder.FindPath(snappedStart, snappedTarget);

            if (currentPath != null && currentPath.Count > 0)
            {
                // Reset before alignment
                pathIndex = 0;

                // Snap the agent to the closest node in new path
                AlignPathToNextForwardNode();

                lastTargetGrid = targetGrid;

                OnPathCalculated?.Invoke(currentPath);
            }
            else
            {
                OnPathFailed?.Invoke();
            }
        }

        repathTimer = 0f;
    }

    /// <summary>
    /// Tracks whether the agent is stuck and forces path recalculation.
    /// </summary>
    private void TrackMovementProgress()
    {
        float moved = Vector3.Distance(transform.position, lastPosition);

        // No movement -> start counting time to force find new way
        if (moved < 0.1f && currentPath != null && pathIndex < currentPath.Count)
        {
            noProgressTimer += Time.deltaTime;
            if (noProgressTimer > 2f)
            {
                ForceFindNewPath();
                noProgressTimer = 0f;
            }
        }
        else
        {
            noProgressTimer = 0f;
        }

        lastPosition = transform.position;
    }

    /// <summary>
    /// Force a new path calculation, even if interval not reached.
    /// </summary>
    private void ForceFindNewPath()
    {
        if (target == null)
        {
            return;
        }

        GridSystem gridSystemInstance = GridSystem.Instance;

        Vector2Int startGrid = gridSystemInstance.GetGridPosition(transform.position);
        Vector2Int targetGrid = gridSystemInstance.GetGridPosition(target.position);

        startGrid = gridSystemInstance.FindNearestWalkable(startGrid);
        targetGrid = gridSystemInstance.FindNearestWalkable(targetGrid);

        List<Vector3> newPath = pathfinder.FindPath(
            gridSystemInstance.GetWorldPosition(startGrid),
            gridSystemInstance.GetWorldPosition(targetGrid)
        );

        if (newPath != null && newPath.Count > 0)
        {
            pathIndex = 0;
            currentPath = newPath;
            AlignPathToNextForwardNode();
            lastTargetGrid = targetGrid;

            OnPathCalculated?.Invoke(newPath);
        }
        else
        {
            Debug.LogWarning("ForceFindNewPath: no valid path found!");

            OnPathFailed?.Invoke();
        }
    }

    /// <summary>
    /// Aligns the agent to the nearest forward-facing node in the path.
    /// </summary>
    private void AlignPathToNextForwardNode()
    {
        if (currentPath == null || currentPath.Count == 0)
        {
            return;
        }

        float bestDistance = float.MaxValue;
        int bestIndex = pathIndex;

        // 90 degree
        float backwardAngleThreshold = 90f;
        float cosThreshold = Mathf.Cos(backwardAngleThreshold * Mathf.Deg2Rad);

        for (int i = 0; i < currentPath.Count; i++)
        {
            // Avoid use the nodes behind
            // So the agent does not snapping back
            Vector3 toNode = (currentPath[i] - transform.position).normalized;
            if (Vector3.Dot(toNode, transform.forward) < cosThreshold)
            {
                continue;
            }

            float distance = Vector3.Distance(transform.position, currentPath[i]);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = i;
            }
        }

        pathIndex = bestIndex;
    }

    //////////////////////////////////////////////////////////////

    #region External API


    public void SetTargetTransform(Transform newTargetTransform) => target = newTargetTransform;

    public void SetTargetPosition(Vector3 target)
    {
        currentPath = pathfinder.FindPath(transform.position, target);
        pathIndex = 0;

        if (currentPath != null && currentPath.Count > 0)
        {
            OnPathCalculated?.Invoke(currentPath);
        }
        else
        {
            OnPathFailed?.Invoke();
        }
    }

    public void ClearTarget() => target = null;

    #endregion


    // Debug Gizmos
    private void OnDrawGizmos()
    {
        if (!enableDrawPath || currentPath == null || currentPath.Count < 2)
        {
            return;
        }

        Gizmos.color = Color.blueViolet;
        for (int i = 0; i < currentPath.Count - 1; i++)
        {
            Gizmos.DrawLine(currentPath[i] + Vector3.up * 0.3f, currentPath[i + 1] + Vector3.up * 0.3f);
        }

        if (currentPath != null && pathIndex < currentPath.Count)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(currentPath[pathIndex] + Vector3.up * 0.3f, 0.2f);
        }
    }
}
