using UnityEngine;

public class EntityView : MonoBehaviour
{
    public EntityId EntityInstance { get; private set; }
    protected World WorldInstance { get; private set; }

    public void Bind(World world, EntityId entity)
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
}
