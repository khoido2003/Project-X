using System.Collections.Generic;
using UnityEngine;

public class EntityViewRegistry : MonoBehaviour
{
    private readonly Dictionary<EntityId, EntityView> _views = new();

    public void Register(EntityView view)
    {
        if (view == null)
        {
            return;
        }

        if (_views.ContainsKey(view.EntityInstance))
        {
            Debug.LogWarning($"EntityView for {view.EntityInstance} already registed");

            return;
        }

        _views[view.EntityInstance] = view;
    }

    public void Unregister(EntityView view)
    {
        if (view == null)
        {
            return;
        }

        _views.Remove(view.EntityInstance);
    }

    public bool TryGetView(EntityId entity, out EntityView view)
    {
        return _views.TryGetValue(entity, out view);
    }
}
