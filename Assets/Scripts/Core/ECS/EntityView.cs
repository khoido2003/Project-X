using UnityEngine;

[DisallowMultipleComponent]
public class EntityView : MonoBehaviour
{
    public EntityId Entity;
    private World _world;

    public void Bind(World world, EntityId entity)
    {
        _world = world;
        Entity = entity;
    }

    private void OnDestroy()
    {
        if (_world != null && _world.Entities.Exists(Entity))
        {
            _world.DestroyEntity(Entity);
        }
    }
}
