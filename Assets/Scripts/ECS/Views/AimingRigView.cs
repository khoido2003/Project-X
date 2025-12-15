using UnityEngine;

public class AimingRigView : EntityView
{
    [Header("Aiming Rig References")]
    [Tooltip(
        "Optional: Spine/upper body bone to rotate for aiming. If null, will rotate the root transform slightly to compensate for animation offset."
    )]
    [SerializeField]
    private Transform spineBone;

    [Header("Aiming Settings")]
    [Tooltip("Speed at which the character rotates to aim (degrees per second).")]
    [SerializeField]
    private float aimRotationSpeed = 360f;

    [Tooltip("Rotation weight/blend (0-1). Higher = more rotation applied.")]
    [SerializeField]
    [Range(0f, 1f)]
    private float rotationWeight = 1f;

    [Header("Continuous Aiming")]
    [Tooltip("If enabled, character will continuously aim at mouse position when not using skills.")]
    [SerializeField]
    private bool continuousAiming = false;

    [Header("Debug")]
    [SerializeField]
    private bool showDebugGizmos = false;

    private CharacterDefinitionSO _characterDef;
    private Vector3 _currentAimTarget;
    private Vector3 _targetAimDirection;
    private bool _isAiming;
    private float _aimDuration;
    private float _aimStartTime;
    private bool _isSkillAiming;

    private Quaternion _initialRotation;
    private Transform _targetTransform;

    private IInputService _inputService;
    private bool _isLocalPlayer;

    public bool IsAiming => _isAiming;

    public override void Bind(World world, EntityId entity)
    {
        base.Bind(world, entity);

        // Try to get character definition from CharacterSelectionComponent
        if (world.Components.TryGet(entity, out CharacterSelectionComponent characterSelection))
        {
            _characterDef = characterSelection.CharacterData;
            ApplyCharacterSettings();
        }

        InitializeTransform();
        TryResolveInput();
        CheckIfLocalPlayer();
    }

    private void Start()
    {
        if (_targetTransform == null)
        {
            InitializeTransform();
        }

        // Try to get character definition if not already set
        if (_characterDef == null && WorldInstance != null)
        {
            if (WorldInstance.Components.TryGet(EntityInstance, out CharacterSelectionComponent characterSelection))
            {
                _characterDef = characterSelection.CharacterData;
                ApplyCharacterSettings();
            }
        }
    }

    private void Update()
    {
        // Handle continuous aiming for ranged characters
        if (continuousAiming && !_isSkillAiming && _characterDef != null && _characterDef.useAimingRig)
        {
            if (_isLocalPlayer && _inputService != null)
            {
                Vector3 mousePos = _inputService.GetMouseWorldPosition();
                if (mousePos.sqrMagnitude > 0.001f)
                {
                    _currentAimTarget = mousePos;
                    _targetAimDirection = (mousePos - transform.position).normalized;
                    _targetAimDirection.y = 0f;
                    ApplyAimingRotation();
                    return;
                }
            }
        }

        if (!_isAiming)
        {
            // Return to neutral position when not aiming
            ReturnToNeutral();
            return;
        }

        UpdateAiming();
    }

    private void InitializeTransform()
    {
        // Use spine bone if assigned, otherwise use root transform
        _targetTransform = spineBone != null ? spineBone : transform;

        // Store initial rotation
        if (_targetTransform != null)
        {
            _initialRotation = _targetTransform.localRotation;
        }
    }

    private void ApplyCharacterSettings()
    {
        if (_characterDef == null)
            return;

        aimRotationSpeed = _characterDef.aimRotationSpeed;

        // Enable continuous aiming if character uses aiming rig
        if (_characterDef.useAimingRig)
        {
            continuousAiming = true;
        }
    }

    private void CheckIfLocalPlayer()
    {
        if (WorldInstance != null && WorldInstance.Components.TryGet(EntityInstance, out NetworkOwnerComponent owner))
        {
            _isLocalPlayer = owner.IsLocalPlayer;
        }
    }

    private void TryResolveInput()
    {
        if (WorldInstance == null)
        {
            return;
        }

        _inputService = WorldInstance.Services.Resolve<IInputService>();
    }

    /// <summary>
    /// Start aiming at a target position
    /// </summary>
    public void StartAiming(Vector3 targetPosition, float duration = 0f)
    {
        _currentAimTarget = targetPosition;
        _isAiming = true;
        _isSkillAiming = true; // Mark as skill-based aiming
        _aimStartTime = Time.time;
        _aimDuration = duration;

        CalculateAimDirection();
    }

    /// <summary>
    /// Start aiming in a specific direction
    /// </summary>
    public void StartAimingDirection(Vector3 direction, float duration = 0f)
    {
        _targetAimDirection = direction.normalized;
        _isAiming = true;
        _isSkillAiming = true; // Mark as skill-based aiming
        _aimStartTime = Time.time;
        _aimDuration = duration;
    }

    /// <summary>
    /// Stop aiming and return to neutral
    /// </summary>
    public void StopAiming()
    {
        _isAiming = false;
        _isSkillAiming = false;
    }

    private void UpdateAiming()
    {
        // Check if aiming duration has expired
        if (_aimDuration > 0f && Time.time - _aimStartTime >= _aimDuration)
        {
            _isSkillAiming = false;
            _isAiming = false;
            return;
        }

        // Calculate aim direction
        if (_currentAimTarget != Vector3.zero)
        {
            Vector3 aimDirection = (_currentAimTarget - transform.position).normalized;
            aimDirection.y = 0f; // Keep horizontal for body rotation
            _targetAimDirection = aimDirection;
        }

        if (_targetAimDirection.sqrMagnitude < 0.01f)
        {
            return;
        }

        // Apply aiming rotation
        ApplyAimingRotation();
    }

    private void CalculateAimDirection()
    {
        if (_currentAimTarget == Vector3.zero)
        {
            // Try to get from input service if local player
            if (_isLocalPlayer && _inputService != null)
            {
                Vector3 mousePos = _inputService.GetMouseWorldPosition();
                _targetAimDirection = (mousePos - transform.position).normalized;
                _targetAimDirection.y = 0f;
            }
            else
            {
                _targetAimDirection = transform.forward;
            }
        }
        else
        {
            _targetAimDirection = (_currentAimTarget - transform.position).normalized;
            _targetAimDirection.y = 0f;
        }
    }

    private void ApplyAimingRotation()
    {
        if (_targetTransform == null)
            return;

        // Calculate the rotation needed to aim at target
        Quaternion targetWorldRotation = Quaternion.LookRotation(_targetAimDirection, Vector3.up);

        // Convert to local space relative to parent
        Transform parent = _targetTransform.parent != null ? _targetTransform.parent : transform;
        Quaternion targetLocalRotation = Quaternion.Inverse(parent.rotation) * targetWorldRotation;

        // Remove pitch and roll, keep only yaw (horizontal rotation)
        Vector3 euler = targetLocalRotation.eulerAngles;
        euler.x = 0f; // No vertical rotation
        euler.z = 0f; // No roll
        targetLocalRotation = Quaternion.Euler(euler);

        // Smoothly rotate toward target
        Quaternion currentRotation = _targetTransform.localRotation;
        Quaternion smoothedRotation = Quaternion.RotateTowards(
            currentRotation,
            targetLocalRotation,
            aimRotationSpeed * Time.deltaTime
        );

        // Blend between current and target rotation based on weight
        _targetTransform.localRotation = Quaternion.Slerp(currentRotation, smoothedRotation, rotationWeight);
    }

    private void ReturnToNeutral()
    {
        if (_targetTransform != null)
        {
            _targetTransform.localRotation = Quaternion.RotateTowards(
                _targetTransform.localRotation,
                _initialRotation,
                aimRotationSpeed * Time.deltaTime
            );
        }
    }

    private void OnDrawGizmos()
    {
        if (!showDebugGizmos || !_isAiming)
            return;

        // Draw aim direction
        Gizmos.color = Color.red;
        Gizmos.DrawRay(transform.position, _targetAimDirection * 5f);

        // Draw target position
        if (_currentAimTarget != Vector3.zero)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(_currentAimTarget, 0.5f);
        }
    }
}
