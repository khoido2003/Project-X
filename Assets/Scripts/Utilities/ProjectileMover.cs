using UnityEngine;

/// <summary>
/// Simple component that moves a GameObject from its current position to a target position over time.
/// Used for visual-only projectiles (like drone attacks).
/// </summary>
public class ProjectileMover : MonoBehaviour
{
    private Vector3 _startPosition;
    private Vector3 _targetPosition;
    private float _duration;
    private float _elapsed;
    private bool _initialized;

    public void Initialize(Vector3 targetPosition, float duration)
    {
        _startPosition = transform.position;
        _targetPosition = targetPosition;
        _duration = Mathf.Max(0.01f, duration);
        _elapsed = 0f;
        _initialized = true;
    }

    private void Update()
    {
        if (!_initialized) return;

        _elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(_elapsed / _duration);

        // Lerp position
        transform.position = Vector3.Lerp(_startPosition, _targetPosition, t);

        // Optional: Look at target
        Vector3 dir = (_targetPosition - _startPosition).normalized;
        if (dir.sqrMagnitude > 0.01f)
        {
            transform.rotation = Quaternion.LookRotation(dir);
        }
    }
}
