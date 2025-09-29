using System;
using System.Collections.Generic;
using System.Net.Http.Headers;
using UnityEngine;

public enum GridLayerName
{
    WALKABLE,
    TERRAIN_COST,
}

public class GridSystem : MonoBehaviour
{
    public static GridSystem Instance { get; private set; }

    [Header("Grid")]
    [SerializeField]
    private Vector2Int gridSize = new Vector2Int(50, 50);

    [SerializeField]
    private float cellSize = 1f;

    [SerializeField]
    private Transform originalPosition;

    private Dictionary<GridLayerName, IGridLayer> layers = new();

    // -----------------------------------

    [Header("Obstacle Detection")]
    [SerializeField]
    private LayerMask obstacleLayer;

    [Tooltip("Half-height of vertical sweep used when detecting obstacles inside a cell")]
    [SerializeField]
    private float obstacleSweepHeight = 1.0f;

    [Tooltip("How far from cell center to consider as obstacle (usually slightly less than half cell)")]
    [SerializeField]
    private float obstacleCheckRadiusFactor = 0.45f;

    //------------------------------------

    [Header("Find Nearest Walkable")]
    [SerializeField]
    private float rayCastHeight = 10f;

    //------------------------------------

    [Header("Grid Debug Visuals")]
    [SerializeField]
    private bool enableVisualGrid = false;

    private GameObject visualContainer;

    [SerializeField]
    private Material walkableMaterial;

    [SerializeField]
    private Material obstacleMaterial;

    private void Awake()
    {
        if (Instance != null)
        {
            Debug.LogError("GridSystem Instance already exist!");

            return;
        }

        Instance = this;

        AddLayer(GridLayerName.WALKABLE, new GridLayer<bool>(gridSize.x, gridSize.y, true));

        AddLayer(GridLayerName.TERRAIN_COST, new GridLayer<float>(gridSize.x, gridSize.y));

        InitializeGrid();

        if (enableVisualGrid)
        {
            visualContainer = new GameObject("GridVisuals");
            visualContainer.transform.parent = transform;
            DrawVisualizeGrid();
        }
    }

    ///////////////////////////////////////////////////////////////////

    #region Initialization & Visuals

    private void InitializeGrid()
    {
        GridLayer<bool> walkable = GetLayer<bool>(GridLayerName.WALKABLE);

        // Sweep box extents defined relative to cell size and obstacleSweepHeight
        float halfCell = cellSize * 0.5f;

        Vector3 halfExtents = new(
            halfCell * obstacleCheckRadiusFactor,
            obstacleSweepHeight * 0.5f,
            halfCell * obstacleCheckRadiusFactor
        );

        for (int x = 0; x < gridSize.x; x++)
        {
            for (int y = 0; y < gridSize.y; y++)
            {
                Vector2Int gridPos = new(x, y);
                Vector3 center = GetWorldPosition(gridPos) + Vector3.up * (obstacleSweepHeight * 0.5f);

                // OverlapBox check is more robust than a single raycast for thin / short obstacles
                bool hasObstacle = Physics.CheckBox(
                    center,
                    halfExtents,
                    Quaternion.identity,
                    obstacleLayer,
                    QueryTriggerInteraction.Ignore
                );

                walkable.SetValue(x, y, !hasObstacle);
            }
        }
    }

    // // OLD WAY TO CHECK OBSTACLES: LESS PRECISE

    // private void InitializeGrid()
    // {
    //     GridLayer<bool> walkableLayer = GetLayer<bool>(GridLayerName.WALKABLE);
    //
    //     for (int x = 0; x < gridSize.x; x++)
    //     {
    //         for (int z = 0; z < gridSize.y; z++)
    //         {
    //             Vector3 worldPos = GetWorldPosition(new Vector2Int(x, z));
    //
    //             Vector3 rayStart = worldPos + Vector3.up * rayCastHeight;
    //
    //             bool isObstackle = Physics.Raycast(rayStart, Vector3.down, rayCastHeight * 2, obstacleLayer);
    //
    //             walkableLayer.SetValue(x, z, !isObstackle);
    //         }
    //     }
    // }

    private void DrawVisualizeGrid()
    {
        // NOTE: creating GameObjects for every cell is fine for small grids and debugging only.
        // For production, replace with a single procedural mesh or Gizmos.
        GridLayer<bool> walkableLayer = GetLayer<bool>(GridLayerName.WALKABLE);
        for (int x = 0; x < gridSize.x; x++)
        {
            for (int z = 0; z < gridSize.y; z++)
            {
                Vector3 worldPos = GetWorldPosition(new Vector2Int(x, z)) + Vector3.down * 0.01f;
                GameObject cellVisual = GameObject.CreatePrimitive(PrimitiveType.Quad);
                cellVisual.transform.parent = visualContainer.transform;
                cellVisual.transform.position = worldPos;
                cellVisual.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                cellVisual.transform.localScale = new Vector3(cellSize, cellSize, 1f);

                Renderer renderer = cellVisual.GetComponent<Renderer>();
                renderer.material = walkableLayer.GetValue(x, z) ? walkableMaterial : obstacleMaterial;
            }
        }
    }

    #endregion

    ///////////////////////////////////////////////////////////////////

    #region Layers API

    public GridLayer<TGridCell> GetLayer<TGridCell>(GridLayerName layerName)
    {
        if (layers.TryGetValue(layerName, out IGridLayer layer))
        {
            return layer as GridLayer<TGridCell>;
        }
        Debug.LogWarning($"Layer {layerName} not found!");
        return null;
    }

    public void AddLayer<TGridCell>(GridLayerName layerName, GridLayer<TGridCell> layer)
    {
        if (layers.ContainsKey(layerName))
        {
            Debug.LogError("GridLayer already exist!");
            return;
        }

        layers[layerName] = layer;
    }

    public void RemoveLayer(GridLayerName layerName)
    {
        if (layers.ContainsKey(layerName))
        {
            layers.Remove(layerName);
        }
    }

    #endregion

    //////////////////////////////////////////////////////////////

    #region Grid helpers

    public Vector3 GetWorldPosition(Vector2Int gridPosition)
    {
        return originalPosition.position + new Vector3(gridPosition.x * cellSize, 0f, gridPosition.y * cellSize);
    }

    public Vector2Int GetGridPosition(Vector3 worldPosition)
    {
        Vector3 localPosition = worldPosition - originalPosition.position;

        return new Vector2Int(
            Mathf.FloorToInt(localPosition.x / cellSize),
            Mathf.FloorToInt(localPosition.z / cellSize)
        );
    }

    public bool IsValidPosition(Vector2Int gridPosition)
    {
        return gridPosition.x >= 0 && gridPosition.x < gridSize.x && gridPosition.y >= 0 && gridPosition.y < gridSize.y;
    }

    public LayerMask GetObstacleLayer()
    {
        return obstacleLayer;
    }

    #endregion


    ////////////////////////////////////////////////////////////////////

    public Vector2Int FindNearestWalkable(Vector2Int origin)
    {
        GridLayer<bool> walkable = GetLayer<bool>(GridLayerName.WALKABLE);

        if (walkable.GetValue(origin.x, origin.y))
        {
            return origin;
        }

        Queue<Vector2Int> frontier = new();
        HashSet<Vector2Int> visited = new();

        frontier.Enqueue(origin);
        visited.Add(origin);

        Vector2Int[] directions =
        {
            new(1, 0),
            new(-1, 0),
            new(0, 1),
            new(0, -1),
            new(1, 1),
            new(1, -1),
            new(-1, 1),
            new(-1, -1),
        };

        while (frontier.Count > 0)
        {
            Vector2Int current = frontier.Dequeue();

            foreach (Vector2Int dir in directions)
            {
                Vector2Int neighbor = current + dir;

                if (!IsValidPosition(neighbor) || visited.Contains(neighbor))
                {
                    continue;
                }

                if (walkable.GetValue(neighbor.x, neighbor.y))
                {
                    return neighbor;
                }

                visited.Add(neighbor);
                frontier.Enqueue(neighbor);
            }
        }
        return origin;
    }
}
