using System;
using System.Collections.Generic;
using UnityEngine;

public enum GridLayerName
{
    WALKABLE,
    TERRAIN_COST,
}

public class GridSystem : MonoBehaviour
{
    public static GridSystem Instance { get; private set; }

    [SerializeField]
    private Vector2Int gridSize = new Vector2Int(50, 50);

    [SerializeField]
    private float cellSize = 1f;

    [SerializeField]
    private LayerMask obstacleLayer;

    [SerializeField]
    private Transform originalPosition;

    [SerializeField]
    private float rayCastHeight = 10f;

    [Header("Grid Debug Visuals")]
    [SerializeField]
    private bool enableVisualGrid = false;

    private GameObject visualContainer;

    [SerializeField]
    private Material walkableMaterial;

    [SerializeField]
    private Material obstacleMaterial;

    private Dictionary<GridLayerName, IGridLayer> layers = new();

    private void Awake()
    {
        if (Instance != null)
        {
            Debug.LogError("GridSystem Instance already exist!");

            return;
        }

        Instance = this;

        visualContainer = new GameObject("GridVisuals");
        visualContainer.transform.parent = transform;

        AddLayer(GridLayerName.WALKABLE, new GridLayer<bool>(gridSize.x, gridSize.y, true));

        AddLayer(GridLayerName.TERRAIN_COST, new GridLayer<float>(gridSize.x, gridSize.y));

        InitializeGrid();

        if (enableVisualGrid)
        {
            DrawVisualizeGrid();
        }
    }

    private void DrawVisualizeGrid()
    {
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

    private void InitializeGrid()
    {
        GridLayer<bool> walkableLayer = GetLayer<bool>(GridLayerName.WALKABLE);

        for (int x = 0; x < gridSize.x; x++)
        {
            for (int z = 0; z < gridSize.y; z++)
            {
                Vector3 worldPos = GetWorldPosition(new Vector2Int(x, z));

                Vector3 rayStart = worldPos + Vector3.up * rayCastHeight;

                bool isObstackle = Physics.Raycast(rayStart, Vector3.down, rayCastHeight * 2, obstacleLayer);

                walkableLayer.SetValue(x, z, !isObstackle);
            }
        }
    }

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
}
