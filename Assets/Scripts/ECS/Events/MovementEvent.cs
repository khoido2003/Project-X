using UnityEngine;

public struct MovementDirectionChangedEvent
{
    public readonly EntityId Entity;
    public readonly Vector3 Direction;

    public MovementDirectionChangedEvent(EntityId entity, Vector3 direction)
    {
        Entity = entity;
        Direction = direction;
    }
}
