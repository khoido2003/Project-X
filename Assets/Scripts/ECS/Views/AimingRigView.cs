using UnityEngine;
using UnityEngine.Animations.Rigging;

/// <summary>
/// Controls character aiming to make them visually aim toward the mouse/target position.
/// Works with Unity's Animation Rigging package.
///
/// Setup Requirements:
/// 1. Add this component to your character prefab
/// 2. Create a Rig Setup in your character:
///    - Add Rig Builder component to the character root
///    - Create a child GameObject named "AimRig"
///    - Add Rig component to AimRig
///    - Create an "AimTarget" child under AimRig with MultiAimConstraint
/// 3. Configure MultiAimConstraint to target the spine/chest bone
/// </summary>
public class AimingRigView : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The Rig component that controls aiming. Auto-detected if not set.")]
    [SerializeField]
    private Rig _aimRig;

    [Tooltip("The transform that the character aims toward. Auto-created if not set.")]
    [SerializeField]
    private Transform _aimTarget;

    [Header("Settings")]
    [Tooltip("How fast the aim weight transitions (0-1 per second)")]
    [SerializeField]
    private float _aimTransitionSpeed = 5f;

    [Tooltip("Maximum weight for the aiming rig (0-1)")]
    [SerializeField]
    private float _maxAimWeight = 1f;

    [Tooltip("If true, continuously aim at mouse position")]
    [SerializeField]
    private bool _continuousAiming = false;

    [Header("Aim Offset Compensation")]
    [Tooltip("Horizontal offset in degrees. Positive = aim more to the right, Negative = aim more to the left")]
    [SerializeField]
    [Range(-45f, 45f)]
    private float _horizontalOffsetDegrees = 0f;

    [Tooltip("Vertical offset in degrees. Positive = aim higher, Negative = aim lower")]
    [SerializeField]
    [Range(-30f, 30f)]
    private float _verticalOffsetDegrees = 0f;

    [Tooltip("Distance offset - move aim target further/closer. Positive = further away")]
    [SerializeField]
    private float _distanceOffset = 0f;

    private float _targetWeight = 0f;
    private float _aimDuration = 0f;
    private float _aimStartTime = 0f;
    private Vector3 _currentAimTarget;
    private bool _isAiming = false;

    // For continuous aiming
    private IInputService _inputService;
    private World _world;
    private EntityView _entityView;
    private bool _isLocalPlayer;

    private void Start()
    {
        _entityView = GetComponent<EntityView>();

        // Auto-detect Rig if not set
        if (_aimRig == null)
        {
            _aimRig = GetComponentInChildren<Rig>();
        }

        // Create aim target if not set
        if (_aimTarget == null && _aimRig != null)
        {
            // Look for existing AimTarget
            var existingTarget = _aimRig.transform.Find("AimTarget");
            if (existingTarget != null)
            {
                _aimTarget = existingTarget;
            }
            else
            {
                // Create a new aim target
                var aimTargetGO = new GameObject("AimTarget");
                aimTargetGO.transform.SetParent(_aimRig.transform);
                aimTargetGO.transform.localPosition = Vector3.forward * 10f;
                _aimTarget = aimTargetGO.transform;
            }
        }

        // Initialize with zero weight
        if (_aimRig != null)
        {
            _aimRig.weight = 0f;
        }

        // Check if this is the local player for continuous aiming
        StartCoroutine(InitializeDelayed());
    }

    private System.Collections.IEnumerator InitializeDelayed()
    {
        // Wait a frame for everything to be set up
        yield return null;

        if (_entityView != null && _entityView.WorldInstance != null)
        {
            _world = _entityView.WorldInstance;
            _inputService = _world.Services.Resolve<IInputService>();

            if (_world.Components.TryGet(_entityView.EntityInstance, out NetworkOwnerComponent owner))
            {
                _isLocalPlayer = owner.IsLocalPlayer;
            }
        }
    }

    private void Update()
    {
        if (_aimRig == null)
        {
            return;
        }

        // Handle continuous aiming for local player
        if (_continuousAiming && _isLocalPlayer && _inputService != null)
        {
            Vector3 mousePos = _inputService.GetMouseWorldPosition();
            if (mousePos.sqrMagnitude > 0.001f)
            {
                // Apply offset compensation and set aim target
                _aimTarget.position = ApplyAimOffset(mousePos);
                _targetWeight = _maxAimWeight;
            }
        }
        // Handle timed aiming (from attacks/skills)
        else if (_isAiming)
        {
            float elapsed = Time.time - _aimStartTime;
            if (elapsed >= _aimDuration)
            {
                // Time expired, stop aiming
                _isAiming = false;
                _targetWeight = 0f;
            }
            else
            {
                // Still aiming
                _targetWeight = _maxAimWeight;
            }
        }
        else
        {
            _targetWeight = 0f;
        }

        // Smoothly transition the rig weight
        _aimRig.weight = Mathf.MoveTowards(_aimRig.weight, _targetWeight, _aimTransitionSpeed * Time.deltaTime);
    }

    /// <summary>
    /// Start aiming at a specific world position for a duration
    /// </summary>
    /// <param name="targetPosition">World position to aim at</param>
    /// <param name="duration">How long to aim (seconds)</param>
    public void StartAiming(Vector3 targetPosition, float duration)
    {
        if (_aimTarget == null)
        {
            Debug.LogWarning("[AimingRigView] No aim target set, cannot aim");
            return;
        }

        // Apply offset compensation and set aim target
        _currentAimTarget = targetPosition;
        _aimTarget.position = ApplyAimOffset(_currentAimTarget);

        _aimDuration = duration;
        _aimStartTime = Time.time;
        _isAiming = true;
        _targetWeight = _maxAimWeight;

        Debug.Log($"[AimingRigView] StartAiming to {_currentAimTarget} for {duration}s");
    }

    /// <summary>
    /// Stop aiming immediately
    /// </summary>
    public void StopAiming()
    {
        _isAiming = false;
        _targetWeight = 0f;
    }

    /// <summary>
    /// Update the aim target position (for moving targets)
    /// </summary>
    public void UpdateAimTarget(Vector3 targetPosition)
    {
        if (_aimTarget != null)
        {
            _currentAimTarget = targetPosition;
            _aimTarget.position = ApplyAimOffset(_currentAimTarget);
        }
    }

    /// <summary>
    /// Apply configured offset to compensate for model/animation aiming issues
    /// </summary>
    private Vector3 ApplyAimOffset(Vector3 worldTarget)
    {
        // Calculate direction from character to target
        Vector3 toTarget = worldTarget - transform.position;
        float distance = toTarget.magnitude;

        if (distance < 0.1f)
        {
            return worldTarget;
        }

        // Apply angular offset
        // Horizontal offset rotates around Y axis
        // Vertical offset rotates around the right axis
        Quaternion horizontalRotation = Quaternion.AngleAxis(_horizontalOffsetDegrees, Vector3.up);

        Vector3 right = Vector3.Cross(Vector3.up, toTarget.normalized);
        Quaternion verticalRotation = Quaternion.AngleAxis(_verticalOffsetDegrees, right);

        // Apply rotations to the direction
        Vector3 offsetDirection = verticalRotation * horizontalRotation * toTarget.normalized;

        // Apply distance offset
        float finalDistance = distance + _distanceOffset;

        return transform.position + offsetDirection * finalDistance;
    }

    /// <summary>
    /// Get the current aim weight
    /// </summary>
    public float GetAimWeight()
    {
        return _aimRig != null ? _aimRig.weight : 0f;
    }

    /// <summary>
    /// Check if currently aiming
    /// </summary>
    public bool IsAiming => _isAiming || _aimRig?.weight > 0.1f;
}
