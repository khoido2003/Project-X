using UnityEngine;

public class TransformComponent
{
    public Vector3 Position;
    public Quaternion Rotation;

    public TransformComponent(Vector3 position, Quaternion rotation)
    {
        Position = position;
        Rotation = rotation;
    }
}
