using System.Collections.Generic;
using UnityEngine;

public class EntityViewRegistry : MonoBehaviour
{
    private readonly Dictionary<EntityId, List<EntityView>> _views = new();

    /// <summary>
    /// Registers an EntityView to the registry under its EntityId.
    /// </summary>
    public void Register(EntityView view)
    {
        if (view == null)
        {
            return;
        }

        if (!_views.TryGetValue(view.EntityInstance, out var list))
        {
            list = new List<EntityView>();
            _views[view.EntityInstance] = list;
        }

        if (list.Contains(view))
        {
            Debug.LogWarning($"EntityView {view.name} already registered for {view.EntityInstance}");
            return;
        }

        list.Add(view);
    }

    /// <summary>
    /// Unregisters a specific EntityView.
    /// </summary>
    public void Unregister(EntityView view)
    {
        if (view == null)
            return;

        if (_views.TryGetValue(view.EntityInstance, out var list))
        {
            list.Remove(view);

            if (list.Count == 0)
            {
                _views.Remove(view.EntityInstance);
            }
        }
    }

    public void Unregister(EntityId entity)
    {
        _views.Remove(entity);
    }

    /// <summary>
    /// Try to get the first EntityView for an entity.
    /// </summary>
    public bool TryGet(EntityId entity, out EntityView view)
    {
        if (_views.TryGetValue(entity, out var list) && list.Count > 0)
        {
            view = list[0];
            return true;
        }

        view = null;
        return false;
    }

    /// <summary>
    /// Gets all EntityViews registered for an entity.
    /// </summary>
    public bool TryGetAll(EntityId entity, out List<EntityView> list)
    {
        return _views.TryGetValue(entity, out list);
    }

    public void Clear()
    {
        _views.Clear();
    }
}
