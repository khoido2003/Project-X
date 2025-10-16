using System.Collections.Generic;
using UnityEngine;

public class EntityViewRegistry : MonoBehaviour
{
    private readonly Dictionary<EntityId, EntityView> _views = new();

    /// <summary>
    /// Registers a new EntityView for its EntityId.
    /// </summary>
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

    /// <summary>
    /// Removes a registered EntityView.
    /// </summary>
    public void Unregister(EntityView view)
    {
        if (view == null)
        {
            return;
        }

        _views.Remove(view.EntityInstance);
    }

    /// <summary>
    /// Attempts to get an EntityView for a given EntityId.
    /// </summary>
    public bool TryGet(EntityId entity, out EntityView view)
    {
        return _views.TryGetValue(entity, out view);
    }

    /// <summary>
    /// Checks if an EntityView exists for a given EntityId.
    /// </summary>
    public bool Has(EntityId entity)
    {
        return _views.ContainsKey(entity);
    }

    /// <summary>
    /// Optionally clear all views (e.g. when resetting the world).
    /// </summary>
    public void Clear()
    {
        _views.Clear();
    }
}
