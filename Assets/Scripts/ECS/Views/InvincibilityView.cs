using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// View component to handle invincibility visual feedback.
/// Swaps character materials to golden appearance during invincibility.
/// </summary>
public class InvincibilityView : EntityView
{
    [Header("Invincibility Visual")]
    [SerializeField]
    [Tooltip("Material to apply during invincibility (golden/shield material)")]
    private Material _invincibilityMaterial;

    [SerializeField]
    [Tooltip("Renderers to apply material to. If empty, will auto-find SkinnedMeshRenderers in children.")]
    private Renderer[] _targetRenderers;

    private Dictionary<Renderer, Material[]> _originalMaterials = new();
    private bool _isInvincible = false;
    private Coroutine _invincibilityCoroutine;

    private void Start()
    {
        // Auto-find renderers if not assigned
        if (_targetRenderers == null || _targetRenderers.Length == 0)
        {
            _targetRenderers = GetComponentsInChildren<SkinnedMeshRenderer>();
        }
    }

    public override void Bind(World world, EntityId entity)
    {
        base.Bind(world, entity);

        Subscribe<InvincibilityStartEvent>(OnInvincibilityStart);
        Subscribe<InvincibilityEndEvent>(OnInvincibilityEnd);
    }

    private void OnDestroy()
    {
        // Restore materials if destroyed while invincible
        RestoreOriginalMaterials();

        if (WorldInstance != null)
        {
            Unsubscribe<InvincibilityStartEvent>(OnInvincibilityStart);
            Unsubscribe<InvincibilityEndEvent>(OnInvincibilityEnd);
        }
    }

    private void OnInvincibilityStart(InvincibilityStartEvent @event)
    {
        if (@event.Entity != EntityInstance)
        {
            return;
        }

        StartInvincibilityVisual(@event.Duration);
    }

    private void OnInvincibilityEnd(InvincibilityEndEvent @event)
    {
        if (@event.Entity != EntityInstance)
        {
            return;
        }

        EndInvincibilityVisual();
    }

    /// <summary>
    /// Start invincibility visual effect with golden material.
    /// Can be called multiple times to refresh the duration.
    /// </summary>
    public void StartInvincibilityVisual(float duration)
    {
        // If already invincible, just refresh the timeout coroutine
        if (_isInvincible)
        {
            // Restart timeout coroutine with new duration
            if (_invincibilityCoroutine != null)
            {
                StopCoroutine(_invincibilityCoroutine);
            }
            _invincibilityCoroutine = StartCoroutine(InvincibilityTimeoutCoroutine(duration + 0.5f));
            return;
        }

        _isInvincible = true;
        ApplyInvincibilityMaterial();

        // Start timeout coroutine as fallback (in case RPC end event is missed)
        if (_invincibilityCoroutine != null)
        {
            StopCoroutine(_invincibilityCoroutine);
        }
        _invincibilityCoroutine = StartCoroutine(InvincibilityTimeoutCoroutine(duration + 0.5f)); // +0.5s buffer
    }


    /// <summary>
    /// End invincibility visual effect and restore original materials.
    /// </summary>
    public void EndInvincibilityVisual()
    {
        if (!_isInvincible)
        {
            return;
        }

        _isInvincible = false;
        RestoreOriginalMaterials();

        if (_invincibilityCoroutine != null)
        {
            StopCoroutine(_invincibilityCoroutine);
            _invincibilityCoroutine = null;
        }
    }

    private IEnumerator InvincibilityTimeoutCoroutine(float timeout)
    {
        yield return new WaitForSeconds(timeout);

        // Fallback timeout - end visual if still active
        if (_isInvincible)
        {
            Debug.LogWarning($"[InvincibilityView] Invincibility visual timed out for {EntityInstance.Id}");
            EndInvincibilityVisual();
        }
    }

    /// <summary>
    /// Stores original materials and applies invincibility material to all renderers.
    /// </summary>
    private void ApplyInvincibilityMaterial()
    {
        if (_targetRenderers == null || _targetRenderers.Length == 0)
        {
            _targetRenderers = GetComponentsInChildren<SkinnedMeshRenderer>();
        }

        if (_invincibilityMaterial == null)
        {
            Debug.LogWarning("[InvincibilityView] No invincibility material assigned!");
            return;
        }

        foreach (var renderer in _targetRenderers)
        {
            if (renderer == null)
            {
                continue;
            }

            // Only store if not already stored (prevents overwriting original with invincibility material)
            if (!_originalMaterials.ContainsKey(renderer))
            {
                _originalMaterials[renderer] = renderer.materials;
            }

            // Apply invincibility material to all material slots
            Material[] invincMats = new Material[renderer.materials.Length];
            for (int i = 0; i < invincMats.Length; i++)
            {
                invincMats[i] = _invincibilityMaterial;
            }
            renderer.materials = invincMats;
        }
    }

    /// <summary>
    /// Restores original materials to all renderers.
    /// </summary>
    private void RestoreOriginalMaterials()
    {
        foreach (var kvp in _originalMaterials)
        {
            if (kvp.Key != null)
            {
                kvp.Key.materials = kvp.Value;
            }
        }
        _originalMaterials.Clear();
    }
}
