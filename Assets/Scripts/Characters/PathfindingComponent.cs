using System.Collections.Generic;
using UnityEngine;

public class PathfindingComponent : MonoBehaviour
{
    private AStartPathfinder pathfinder = new();

    private List<Vector3> currentPath;
    private int pathIndex;

    [SerializeField]
    private Character character;

    [SerializeField]
    private float moveSpeed = 3f;

    [SerializeField]
    private float repathInterval = 0.5f;

    private float repathTimer;

    private void Update()
    {
        // Recalculate path every X seconds so it follows moving target
        repathTimer += Time.deltaTime;
        if (repathTimer >= repathInterval && character != null)
        {
            SetTarget(character.transform.position);
            repathTimer = 0f;
        }

        Move();
    }

    private void Move()
    {
        if (currentPath != null && pathIndex < currentPath.Count)
        {
            Vector3 nextPos = currentPath[pathIndex];

            Vector3 moveDir = (nextPos - transform.position).normalized;
            transform.position += moveDir * moveSpeed * Time.deltaTime;

            if (Vector3.Distance(transform.position, nextPos) < 0.1f)
            {
                pathIndex++;
            }
        }
    }

    public void SetTarget(Vector3 target)
    {
        currentPath = pathfinder.FindPath(transform.position, target);
        pathIndex = 0;
    }
}
