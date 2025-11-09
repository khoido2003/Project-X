using UnityEngine;

/// <summary>
/// Visualizes the patrol waypoints and current path for an enemy entity.
/// Works with ECS data (EnemyPatrolComponent, EnemyPathComponent).
/// </summary>
[DisallowMultipleComponent]
public class EnemyPatrolPointGizmo : EntityView
{
    [Header("Gizmo Settings")]
    public Color patrolColor = Color.yellow;
    public Color pathColor = Color.cyan;
    public float pointSize = 0.2f;

    private void OnDrawGizmos()
    {
        if (WorldInstance == null || EntityInstance.Equals(default))
            return;

        if (WorldInstance.Components.TryGet(EntityInstance, out EnemyComponent enemy))
        {
            Gizmos.color = patrolColor;

            for (int i = 0; i < enemy.PatrolWaypoints.Count; i++)
            {
                Vector3 p = enemy.PatrolWaypoints[i];
                Gizmos.DrawSphere(p + Vector3.up * 0.1f, pointSize);

                if (i < enemy.PatrolWaypoints.Count - 1)
                    Gizmos.DrawLine(p, enemy.PatrolWaypoints[i + 1]);
            }

            if (enemy.PatrolWaypoints.Count > 1)
                Gizmos.DrawLine(enemy.PatrolWaypoints[^1], enemy.PatrolWaypoints[0]);
        }

        if (enemy.HasPath)
        {
            Gizmos.color = pathColor;

            for (int i = 0; i < enemy.Path.Count; i++)
            {
                Vector3 p = enemy.Path[i];
                Gizmos.DrawCube(p + Vector3.up * 0.2f, Vector3.one * pointSize * 0.5f);

                if (i < enemy.Path.Count - 1)
                    Gizmos.DrawLine(p, enemy.Path[i + 1]);
            }
        }
    }
}
