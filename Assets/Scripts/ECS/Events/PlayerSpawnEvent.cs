using UnityEngine;

public struct PlayerSpawnEvent
{
    public readonly EntityId Entity;
    public readonly GameObject PlayerObject;
    public readonly Transform Transform;

    public PlayerSpawnEvent(EntityId entity, GameObject playerObject, Transform transform)
    {
        Entity = entity;
        PlayerObject = playerObject;
        Transform = transform;
    }
}
