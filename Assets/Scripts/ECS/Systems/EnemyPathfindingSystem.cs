using System.Collections.Generic;
using UnityEngine;

public class EnemyPathfindingSystem : ISystem
{
    private World _world;
    private AStarPathfinder _pathfinder;

    public void Initialize(World world)
    {
        _world = world;
        _pathfinder = new AStarPathfinder();
        _world.Events.Subscribe<EnemyPathRequestEvent>(OnEnemyPathRequest);
    }

    private void OnEnemyPathRequest(EnemyPathRequestEvent e)
    {
        if (!_world.Entities.Exists(e.Entity))
            return;

        if (!_world.Components.TryGet(e.Entity, out TransformComponent trans))
            return;

        if (!_world.Components.TryGet(e.Entity, out EnemyComponent enemy))
        {
            enemy = new EnemyComponent();
            _world.Components.Add(e.Entity, enemy);
        }

        GridSystem grid = GridSystem.Instance;

        Vector2Int startGrid = grid.GetGridPosition(trans.Position);
        Vector2Int targetGrid = grid.GetGridPosition(e.Target);

        startGrid = grid.FindNearestWalkable(startGrid);
        targetGrid = grid.FindNearestWalkable(targetGrid);

        Vector3 startPos = grid.GetWorldPosition(startGrid);
        Vector3 targetPos = grid.GetWorldPosition(targetGrid);

        List<Vector3> result = _pathfinder.FindPath(startPos, targetPos);

        if (result == null || result.Count == 0)
        {
            enemy.Path = null;
            enemy.WaypointIndex = 0;
            return;
        }

        // Match agent Y
        float agentY = trans.Position.y;
        for (int i = 0; i < result.Count; i++)
        {
            Vector3 p = result[i];
            p.y = agentY;
            result[i] = p;
        }

        // Align to forward node
        int startIndex = AlignForwardNode(result, trans.Position, trans.Rotation);

        enemy.Path = result;
        enemy.WaypointIndex = Mathf.Clamp(startIndex, 0, result.Count - 1);
        enemy.LastRequestedTarget = targetPos;
        enemy.LastRequestTime = Time.time;
        enemy.StoppingDistance = e.StoppingDistance;

        _world.Events.Publish(new EnemyPathCalculatedEvent(e.Entity));
    }

    private int AlignForwardNode(List<Vector3> path, Vector3 pos, Quaternion rot)
    {
        if (path == null || path.Count == 0)
            return 0;

        Vector3 forward = rot * Vector3.forward;
        float cosThreshold = Mathf.Cos(90f * Mathf.Deg2Rad);

        int best = 0;
        float bestDist = float.MaxValue;

        for (int i = 0; i < path.Count; i++)
        {
            Vector3 dir = (path[i] - pos).normalized;
            if (Vector3.Dot(forward, dir) < cosThreshold)
                continue;

            float dist = Vector3.Distance(pos, path[i]);
            if (dist < bestDist)
            {
                bestDist = dist;
                best = i;
            }
        }
        return best;
    }

    public void Update(float dt) { }

    public void FixedUpdate(float dt) { }

    public void Shutdown()
    {
        _world.Events.Unsubscribe<EnemyPathRequestEvent>(OnEnemyPathRequest);
    }
}
