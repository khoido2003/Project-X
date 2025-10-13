using System;
using UnityEngine;

public class EntityView : MonoBehaviour
{
    public EntityId EntityInstance { get; private set; }
    public World WorldInstance { get; private set; }

    public virtual void Bind(World world, EntityId entity)
    {
        WorldInstance = world;
        EntityInstance = entity;
    }

    private void OnDestroy()
    {
        if (WorldInstance == null || !WorldInstance.Entities.Exists(EntityInstance))
        {
            return;
        }

        try
        {
            WorldInstance.DestroyEntity(EntityInstance);
        }
        catch (System.Exception ex)
        {
            UnityEngine.Debug.LogWarning($"Failed to destroy entity {EntityInstance}: {ex.Message}");
        }
    }

    protected void Subscribe<T>(Action<T> handler)
        where T : struct
    {
        WorldInstance.Events.Subscribe(handler);
    }

    protected void Unsubscribe<T>(Action<T> handler)
        where T : struct
    {
        WorldInstance.Events.Unsubscribe(handler);
    }
}
