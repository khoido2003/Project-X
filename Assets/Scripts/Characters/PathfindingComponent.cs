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
    private float waypointTolerance = 0.25f;

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

            transform.position = Vector3.MoveTowards(transform.position, nextPos, moveSpeed * Time.deltaTime);

            if (Vector3.Distance(transform.position, nextPos) < 0.1f)
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

            Vector3 snappedTarget = gridSystemInstance.GetWorldPosition(targetGrid);

            if (Vector3.Distance(snappedTarget, gridSystemInstance.GetWorldPosition(lastTargetGrid)) > recalcThreshold)
            {
                Vector3 snappedStart = gridSystemInstance.GetWorldPosition(startGrid);

                currentPath = pathfinder.FindPath(snappedStart, snappedTarget);

                if (currentPath != null && currentPath.Count > 0)
                {
                    // Snap the agent to the closest node in new path
                    AlignPathToClosedNode();

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
        if (moved < 0.05f)
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

        currentPath = pathfinder.FindPath(
            gridSystemInstance.GetWorldPosition(startGrid),
            gridSystemInstance.GetWorldPosition(targetGrid)
        );

        if (currentPath != null && currentPath.Count > 0)
        {
            AlignPathToClosedNode();
            lastTargetGrid = targetGrid;
            pathIndex = 0;
        }
    }

    private void AlignPathToClosedNode()
    {
        if (currentPath == null || currentPath.Count == 0)
        {
            return;
        }

        float bestDistance = float.MaxValue;
        int bestIndex = 0;

        for (int i = 0; i < currentPath.Count; i++)
        {
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
    }
}
