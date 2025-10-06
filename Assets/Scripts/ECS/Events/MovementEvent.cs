using UnityEngine;

public struct MovementStartedEvent
{
    public readonly EntityId Entity;

    public MovementStartedEvent(EntityId entity) => Entity = entity;
}

public struct MovementStoppedEvent
{
    public readonly EntityId Entity;

    public MovementStoppedEvent(EntityId entity) => Entity = entity;
}

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
