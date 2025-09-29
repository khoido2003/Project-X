using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

public class PathfindingComponent : MonoBehaviour
{
    private AStartPathfinder pathfinder = new();
    private List<Vector3> currentPath;
    private int pathIndex;

    [SerializeField]
    private Transform target;

    [SerializeField]
    private float moveSpeed = 3f;

    [SerializeField]
    private float repathInterval = 1f;

    [SerializeField]
    private float waypointTolerance = 0.5f;

    [SerializeField]
    private bool enableDrawPath = true;

    private Vector3 lastPosition;
    private float noProgressTimer = 0f;
    private float recalcThreshold = 0.5f;
    private float repathTimer;
    private float stuckTimer = 0f;
    private Vector2Int lastTargetGrid;

    private void Update()
    {
        TrackMovementProgress();
        FindTarget();
        Move();
    }

    private void Move()
    {
        if (currentPath != null && pathIndex < currentPath.Count)
        {
            Vector3 nextPos = currentPath[pathIndex];
            Vector3 moveDir = (nextPos - transform.position).normalized;

            // DON'T do this: this cause the object move like a robot
            // transform.position += moveDir * moveSpeed * Time.deltaTime;

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

            transform.position = Vector3.MoveTowards(transform.position, nextPos, moveSpeed * Time.deltaTime);

            if (Vector3.Distance(transform.position, nextPos) < waypointTolerance)
            {
                pathIndex++;

                stuckTimer = 0f;
            }
            else
            {
                stuckTimer += Time.deltaTime;
                if (stuckTimer > 1.5f)
                {
                    // Stuck too long -> Find new path to the target
                    ForceFindNewPath();
                    stuckTimer = 0f;
                }
            }
        }
    }

    private void FindTarget()
    {
        if (target == null)
        {
            return;
        }

        // Recalculate path every X seconds so it follows moving target
        repathTimer += Time.deltaTime;

        if (repathTimer >= repathInterval)
        {
            GridSystem gridSystemInstance = GridSystem.Instance;

            Vector2Int startGrid = gridSystemInstance.GetGridPosition(transform.position);

            Vector2Int targetGrid = gridSystemInstance.GetGridPosition(target.position);

            GridLayer<bool> walkable = gridSystemInstance.GetLayer<bool>(GridLayerName.WALKABLE);

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
                }
            }

            repathTimer = 0f;
        }
    }

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
        }
        else
        {
            Debug.LogWarning("ForceFindNewPath: no valid path found!");
        }
    }

    private void AlignPathToNextForwardNode()
    {
        if (currentPath == null || currentPath.Count == 0)
        {
            return;
        }

        float bestDistance = float.MaxValue;
        int bestIndex = pathIndex;

        for (int i = 0; i < currentPath.Count; i++)
        {
            // Avoid use the nodes behind
            // So the agent does not snapping back
            if (Vector3.Dot((currentPath[i] - transform.position).normalized, transform.forward) < -0.2f)
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

    public void SetTarget(Vector3 target)
    {
        currentPath = pathfinder.FindPath(transform.position, target);
        pathIndex = 0;
    }

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
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(currentPath[pathIndex] + Vector3.up * 0.3f, 0.2f);
        }
    }
}
