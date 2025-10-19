using UnityEngine;

public class EnemyPatrolPoints : MonoBehaviour
{
    [SerializeField]
    private int numberOfPoints = 4;

    [SerializeField]
    private float radius = 4f;

    [SerializeField]
    private float minPointDistance = 2f;

    private Transform[] patrolPoints;

    public Transform[] GeneratePatrolPoints()
    {
        patrolPoints = new Transform[numberOfPoints];

        for (int i = 0; i < numberOfPoints; i++)
        {
            Vector3 worldPos;
            int maxAttempts = 20;
            int attemp = 0;

            do
            {
                Vector3 randomPos = transform.position + Random.insideUnitSphere * radius;
                randomPos.y = transform.position.y;

                Vector2Int gridPos = GridSystem.Instance.GetGridPosition(randomPos);

                gridPos = GridSystem.Instance.FindNearestWalkable(gridPos);

                worldPos = GridSystem.Instance.GetWorldPosition(gridPos);

                attemp++;
            } while (IsTooClose(worldPos, i) && attemp < maxAttempts);

            GameObject point = new GameObject($"PatrolPoint_{i}");
            point.transform.position = worldPos;
            patrolPoints[i] = point.transform;
        }

        return patrolPoints;
    }

    private bool IsTooClose(Vector3 newPoint, int currentCount)
    {
        for (int j = 0; j < currentCount; j++)
        {
            if (Vector3.Distance(newPoint, patrolPoints[j].position) < minPointDistance)
            {
                return true;
            }
        }

        return false;
    }

    public float GetPatrolRadius()
    {
        return radius;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;

        if (patrolPoints != null)
        {
            foreach (var point in patrolPoints)
            {
                if (point != null)
                    Gizmos.DrawSphere(point.position, 0.3f);
            }
        }

        // Also draw patrol radius for debugging
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
