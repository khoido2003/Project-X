using UnityEngine;

public interface IGridLayer { }

public class GridLayer<TGridCell> : IGridLayer
{
    private TGridCell[,] gridData;
    private TGridCell defaultValue;

    public GridLayer(int width, int height, TGridCell defaultValue = default)
    {
        gridData = new TGridCell[width, height];

        this.defaultValue = defaultValue;

        Initialize();
    }

    private void Initialize()
    {
        for (int x = 0; x < gridData.GetLength(0); x++)
        {
            for (int y = 0; y < gridData.GetLength(1); y++)
            {
                gridData[x, y] = defaultValue;
            }
        }
    }

    public TGridCell GetValue(int x, int y)
    {
        return gridData[x, y];
    }

    public void SetValue(int x, int y, TGridCell value)
    {
        gridData[x, y] = value;
    }
}
