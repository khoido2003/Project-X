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
                Vector3 directionBetweenTwoPoint = (rawPath[i] - rawPath[currentIndex]).normalized;

                float distanceBetweenTwoPoint = Vector3.Distance(rawPath[i], rawPath[currentIndex]);

                float agentScanRadius = 0.25f;

                if (
                    !Physics.CapsuleCast(
                        rawPath[currentIndex] + Vector3.up * 0.5f,
                        rawPath[currentIndex] + Vector3.up * 1.5f,
                        agentScanRadius,
                        directionBetweenTwoPoint,
                        distanceBetweenTwoPoint,
                        GridSystem.Instance.GetObstacleLayer()
                    )
                )
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

    private List<Vector2Int> GetNeighbors(Vector2Int position)
    {
        List<Vector2Int> neighbors = new();

        for (int x = -1; x <= 1; x++)
        {
            for (int z = -1; z <= 1; z++)
            {
                if (x == 0 && z == 0)
                {
                    continue;
                }

                neighbors.Add(new Vector2Int(position.x + x, position.y + z));
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
