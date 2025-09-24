using UnityEngine;

public static class Drawer
{
    /// <summary>
    /// Creates a LineRenderer circle.
    /// </summary>
    /// <param name="radius">Radius of the circle.</param>
    /// <param name="segments">Number of segments (higher = smoother).</param>
    /// <param name="width">Line width.</param>
    /// <param name="color">Line color.</param>
    /// <returns>The created LineRenderer.</returns>
    public static LineRenderer CreateCircle(float radius, int segments, float width, Color color)
    {
        GameObject go = new GameObject("Circle");
        LineRenderer lr = go.AddComponent<LineRenderer>();

        lr.loop = true;
        lr.useWorldSpace = false;
        lr.widthMultiplier = width;
        lr.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = lr.endColor = color;

        Vector3[] points = new Vector3[segments];
        for (int i = 0; i < segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            points[i] = new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
        }
        lr.positionCount = segments;
        lr.SetPositions(points);

        return lr;
    }
}
