using System.Collections.Generic;
using UnityEngine;

public class AStartPathfinder
{
    private class Node
    {
        public Vector2Int Position { get; set; }
        public Node Parent { get; set; }
        public float GCost { get; set; }
        public float HCost { get; set; }
        public float FCost => GCost + HCost;
    }

    public List<Vector3> FindPath(Vector3 startWWorld, Vector3 endWorld)
    {
        GridSystem gridSystem = GridSystem.Instance;

        if (gridSystem == null)
        {
            Debug.LogError("GridSystem not found!");

            return null;
        }

        Vector2Int start = gridSystem.GetGridPosition(startWWorld);
        Vector2Int end = gridSystem.GetGridPosition(endWorld);

        if (!gridSystem.IsValidPosition(start) || !gridSystem.IsValidPosition(end))
        {
            return null;
        }

        GridLayer<bool> walkableLayer = gridSystem.GetLayer<bool>(GridLayerName.WALKABLE);

        GridLayer<float> costLayer = gridSystem.GetLayer<float>(GridLayerName.TERRAIN_COST);

        if (walkableLayer == null || !walkableLayer.GetValue(start.x, start.y) || !walkableLayer.GetValue(end.x, end.y))
        {
            Debug.Log("Node invalid or walkableLayer is null!");
            return null;
        }

        PriorityQueue<Node> pq = new();

        Dictionary<Vector2Int, Node> openSetMap = new();
        HashSet<Vector2Int> closeSet = new();

        Node startNode = new Node
        {
            Position = start,
            GCost = 0,
            HCost = GetHeuristic(start, end),
        };

        pq.Enqueue(startNode, startNode.FCost);
        openSetMap[start] = startNode;

        while (pq.Count > 0)
        {
            Node currentNode = pq.Dequeue();
            openSetMap.Remove(currentNode.Position);

            if (currentNode.Position == end)
            {
                List<Vector3> rawPath = RetracePath(currentNode);

                // MUST do this to avoid zig-zag or zittering path
                return SmoothPath(rawPath);
            }

            closeSet.Add(currentNode.Position);

            foreach (Vector2Int neighborPos in GetNeighbors(currentNode.Position))
            {
                if (
                    !gridSystem.IsValidPosition(neighborPos)
                    || !walkableLayer.GetValue(neighborPos.x, neighborPos.y)
                    || closeSet.Contains(neighborPos)
                )
                {
                    continue;
                }

                float terrainCost = costLayer?.GetValue(neighborPos.x, neighborPos.y) ?? 1f;

                float moveCost = CalcOctileDistance(currentNode.Position, neighborPos) * terrainCost;

                float newGCost = currentNode.GCost + moveCost;

                if (openSetMap.TryGetValue(neighborPos, out Node existingNeighbor))
                {
                    if (newGCost < existingNeighbor.GCost)
                    {
                        existingNeighbor.GCost = newGCost;
                        existingNeighbor.Parent = currentNode;
                        pq.Enqueue(existingNeighbor, existingNeighbor.FCost);
                    }
                }
                else
                {
                    Node neighbor = new Node { Position = neighborPos };

                    neighbor.Parent = currentNode;
                    neighbor.GCost = newGCost;
                    neighbor.HCost = GetHeuristic(neighborPos, end);
                    pq.Enqueue(neighbor, neighbor.FCost);

                    openSetMap[neighborPos] = neighbor;
                }
            }
        }

        return null;
    }

    private List<Vector3> SmoothPath(List<Vector3> rawPath)
    {
        if (rawPath == null || rawPath.Count < 2)
        {
            return rawPath;
        }

        List<Vector3> smoothPath = new();

        smoothPath.Add(rawPath[0]);

        int currentIndex = 0;

        while (currentIndex < rawPath.Count - 1)
        {
            int nextIndex = rawPath.Count - 1;

            for (int i = rawPath.Count - 1; i >= currentIndex; i--)
            {
                if (HasNextNodeOfSight(rawPath[currentIndex], rawPath[i]))
                {
                    nextIndex = i;
                    break;
                }
            }

            if (nextIndex == currentIndex)
            {
                break;
            }

            smoothPath.Add(rawPath[nextIndex]);
            currentIndex = nextIndex;
        }

        return smoothPath;
    }

    private bool HasNextNodeOfSight(Vector3 a, Vector3 b)
    {
        Vector3 direction = (b - a).normalized;
        float distance = Vector3.Distance(a, b);
        float radius = 0.25f;

        float capsuleStart = 0.5f;
        float capsuleEnd = 1.5f;

        return !Physics.CapsuleCast(
            a + Vector3.up * capsuleStart,
            a + Vector3.up * capsuleEnd,
            radius,
            direction,
            distance,
            GridSystem.Instance.GetObstacleLayer()
        );
    }

    private List<Vector2Int> GetNeighbors(Vector2Int position)
    {
        List<Vector2Int> neighbors = new();
        GridSystem gridSystemInstance = GridSystem.Instance;

        GridLayer<bool> walkable = gridSystemInstance.GetLayer<bool>(GridLayerName.WALKABLE);

        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                if (x == 0 && y == 0)
                {
                    continue;
                }

                Vector2Int node = new(position.x + x, position.y + y);

                if (!gridSystemInstance.IsValidPosition(node) || !walkable.GetValue(node.x, node.y))
                {
                    continue;
                }

                // Check for diaonal corner cutting
                if (x != 0 && y != 0)
                {
                    Vector2Int node1 = new(position.x + x, position.y);
                    Vector2Int node2 = new(position.x, position.y + y);

                    if (!walkable.GetValue(node1.x, node1.y) || !walkable.GetValue(node2.x, node2.y))
                    {
                        continue;
                    }
                }

                neighbors.Add(node);
            }
        }

        return neighbors;
    }

    private List<Vector3> RetracePath(Node endNode)
    {
        List<Vector3> path = new();

        Node currentNode = endNode;

        while (currentNode != null)
        {
            path.Add(GridSystem.Instance.GetWorldPosition(currentNode.Position));

            currentNode = currentNode.Parent;
        }

        path.Reverse();
        return path;
    }

    private float GetHeuristic(Vector2Int a, Vector2Int b)
    {
        return CalcOctileDistance(a, b);
    }

    // 8 directions
    private float CalcOctileDistance(Vector2Int a, Vector2Int b)
    {
        int dx = Mathf.Abs(a.x - b.x);
        int dy = Mathf.Abs(a.y - b.y);
        return Mathf.Max(dx, dy) + (Mathf.Sqrt(2) - 1) * Mathf.Min(dx, dy);
    }

    // 4 directions
    private float CalcManhattanDistance(Vector2Int a, Vector2Int b)
    {
        int dx = Mathf.Abs(a.x - b.x);
        int dz = Mathf.Abs(a.y - b.y);

        return dx + dz;
    }
}
