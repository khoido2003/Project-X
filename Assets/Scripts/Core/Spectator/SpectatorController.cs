using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Controls spectator camera with two modes:
/// 1. Overview Mode - Free fly camera with WASD + mouse
/// 2. Player Follow Mode - Follow a player, arrow keys to switch
///
/// Press Tab to toggle between modes.
/// This is a LOCAL-ONLY component, not networked.
/// </summary>
public class SpectatorController : MonoBehaviour
{
    public enum SpectatorMode
    {
        Overview,
        PlayerFollow,
    }

    [Header("Mode Settings")]
    [SerializeField]
    private SpectatorMode _currentMode = SpectatorMode.Overview;

    [Header("Overview Mode - Free Fly")]
    [SerializeField]
    private float _flySpeed = 15f;

    [SerializeField]
    private float _flySpeedFast = 30f;

    [SerializeField]
    private float _mouseSensitivity = 2f;

    [SerializeField]
    private float _smoothTime = 0.1f;

    [Header("Player Follow Mode")]
    [SerializeField]
    private Vector3 _followOffset = new Vector3(0f, 8f, -6f);

    [SerializeField]
    private float _followSmoothSpeed = 5f;

    [Header("Bounds")]
    [SerializeField]
    private float _minHeight = 2f;

    [SerializeField]
    private float _maxHeight = 50f;

    // Internal state
    private World _world;
    private Camera _camera;
    private List<EntityId> _playerEntities = new();
    private int _currentPlayerIndex = 0;
    private Transform _followTarget;

    // Smooth movement
    private Vector3 _velocity;
    private float _rotationX;
    private float _rotationY;

    // Input cooldowns
    private float _switchCooldown = 0f;
    private const float SWITCH_COOLDOWN_TIME = 0.3f;

    // Player list refresh
    private float _refreshTimer = 0f;
    private const float REFRESH_INTERVAL = 1f;

    public SpectatorMode CurrentMode => _currentMode;
    public string FollowingPlayerName { get; private set; } = "";

    // Events for UI
    public Action<SpectatorMode> OnModeChanged;
    public Action<string> OnFollowTargetChanged;

    private void Awake()
    {
        _camera = GetComponentInChildren<Camera>() ?? Camera.main;
    }

    private void Start()
    {
        // Initialize rotation from current camera orientation
        Vector3 euler = transform.eulerAngles;
        _rotationX = euler.y;
        _rotationY = euler.x;

        // Wait for world to initialize
        StartCoroutine(InitializeWhenReady());
    }

    private IEnumerator InitializeWhenReady()
    {
        while (WorldRunner.Instance == null || WorldRunner.Instance.World == null)
        {
            yield return null;
        }

        _world = WorldRunner.Instance.World;
        RefreshPlayerList();

        Debug.Log("[SpectatorController] Initialized - Press Tab to switch modes");
    }

    private void Update()
    {
        if (_world == null)
        {
            return;
        }

        // Update cooldowns
        if (_switchCooldown > 0f)
        {
            _switchCooldown -= Time.deltaTime;
        }

        // Refresh player list periodically
        _refreshTimer += Time.deltaTime;

        if (_refreshTimer >= REFRESH_INTERVAL)
        {
            _refreshTimer = 0f;
            RefreshPlayerList();
        }

        // Mode toggle
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            ToggleMode();
        }

        // Mode-specific update
        switch (_currentMode)
        {
            case SpectatorMode.Overview:
                UpdateOverviewMode();
                break;
            case SpectatorMode.PlayerFollow:
                UpdatePlayerFollowMode();
                break;
        }
    }

    private void ToggleMode()
    {
        _currentMode = _currentMode == SpectatorMode.Overview ? SpectatorMode.PlayerFollow : SpectatorMode.Overview;

        if (_currentMode == SpectatorMode.PlayerFollow)
        {
            // Try to find a player to follow
            if (_playerEntities.Count > 0)
            {
                _currentPlayerIndex = 0;
                UpdateFollowTarget();
            }
        }

        OnModeChanged?.Invoke(_currentMode);
        Debug.Log($"[SpectatorController] Switched to {_currentMode} mode");
    }

    #region Overview Mode

    private void UpdateOverviewMode()
    {
        // Mouse look (only when right mouse button held)
        if (Input.GetMouseButton(1))
        {
            _rotationX += Input.GetAxis("Mouse X") * _mouseSensitivity;
            _rotationY -= Input.GetAxis("Mouse Y") * _mouseSensitivity;
            _rotationY = Mathf.Clamp(_rotationY, -89f, 89f);

            transform.rotation = Quaternion.Euler(_rotationY, _rotationX, 0f);

            // Hide cursor while looking
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        // WASD movement on XZ plane (horizontal movement regardless of camera angle)
        float speed = Input.GetKey(KeyCode.LeftShift) ? _flySpeedFast : _flySpeed;

        Vector3 input = Vector3.zero;
        
        // Get horizontal forward/right vectors (ignore Y component)
        Vector3 flatForward = new Vector3(transform.forward.x, 0f, transform.forward.z).normalized;
        Vector3 flatRight = new Vector3(transform.right.x, 0f, transform.right.z).normalized;
        
        // If we're looking straight down, use world forward as reference
        if (flatForward.sqrMagnitude < 0.01f)
        {
            flatForward = Vector3.forward;
            flatRight = Vector3.right;
        }

        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
        {
            input += flatForward;
        }
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
        {
            input -= flatForward;
        }
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
        {
            input -= flatRight;
        }
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
        {
            input += flatRight;
        }
        
        // Vertical movement with E/Q or Space/Ctrl
        if (Input.GetKey(KeyCode.E) || Input.GetKey(KeyCode.Space))
        {
            input += Vector3.up;
        }
        if (Input.GetKey(KeyCode.Q) || Input.GetKey(KeyCode.LeftControl))
        {
            input -= Vector3.up;
        }

        if (input.sqrMagnitude > 0.01f)
        {
            input = input.normalized * speed;
        }

        // Smooth movement
        Vector3 targetVelocity = input;
        _velocity = Vector3.Lerp(_velocity, targetVelocity, Time.deltaTime / _smoothTime);

        Vector3 newPos = transform.position + _velocity * Time.deltaTime;
        
        // Mouse scroll wheel zoom (move camera up/down)
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.01f)
        {
            float zoomSpeed = 20f;
            newPos.y -= scroll * zoomSpeed; // Scroll up = zoom in (move down), scroll down = zoom out (move up)
        }

        // Clamp height
        newPos.y = Mathf.Clamp(newPos.y, _minHeight, _maxHeight);

        transform.position = newPos;
    }

    #endregion

    #region Player Follow Mode

    private void UpdatePlayerFollowMode()
    {
        // Arrow keys to switch players
        if (_switchCooldown <= 0f)
        {
            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
            {
                CyclePlayer(1);
                _switchCooldown = SWITCH_COOLDOWN_TIME;
            }
            else if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
            {
                CyclePlayer(-1);
                _switchCooldown = SWITCH_COOLDOWN_TIME;
            }
        }

        // Follow the target
        if (_followTarget != null)
        {
            Vector3 targetPos = _followTarget.position + _followOffset;
            transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * _followSmoothSpeed);

            // Look at player
            Quaternion targetRot = Quaternion.LookRotation(_followTarget.position - transform.position);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * _followSmoothSpeed);
        }
        else
        {
            // No target - try to find one
            if (_playerEntities.Count > 0)
            {
                UpdateFollowTarget();
            }
        }
    }

    private void CyclePlayer(int direction)
    {
        if (_playerEntities.Count == 0)
        {
            return;
        }

        _currentPlayerIndex = (_currentPlayerIndex + direction + _playerEntities.Count) % _playerEntities.Count;
        UpdateFollowTarget();
    }

    private void UpdateFollowTarget()
    {
        if (_currentPlayerIndex >= _playerEntities.Count)
        {
            _currentPlayerIndex = 0;
        }

        if (_playerEntities.Count == 0)
        {
            _followTarget = null;
            FollowingPlayerName = "";
            OnFollowTargetChanged?.Invoke("");
            return;
        }

        EntityId targetEntity = _playerEntities[_currentPlayerIndex];

        // Get the transform
        var registry = _world.Services.Resolve<EntityViewRegistry>();
        if (registry != null && registry.TryGet(targetEntity, out EntityView view))
        {
            _followTarget = view.transform;

            if (
                _world.Components.TryGet(targetEntity, out CharacterSelectionComponent charSel)
                && charSel.CharacterData != null
            )
            {
                FollowingPlayerName = charSel.CharacterData.characterName;
            }
            else
            {
                FollowingPlayerName = $"Player {_currentPlayerIndex + 1}";
            }

            OnFollowTargetChanged?.Invoke(FollowingPlayerName);
            Debug.Log($"[SpectatorController] Now following: {FollowingPlayerName}");
        }
    }

    #endregion

    #region Player List Management

    private void RefreshPlayerList()
    {
        if (_world == null)
        {
            return;
        }

        _playerEntities.Clear();

        foreach (var (entity, _, health) in _world.Components.Query<PlayerTagComponent, HealthDataComponent>())
        {
            // Only include alive players
            if (!health.IsDead)
            {
                _playerEntities.Add(entity);
            }
        }

        // Validate current index
        if (_currentPlayerIndex >= _playerEntities.Count)
        {
            _currentPlayerIndex = Mathf.Max(0, _playerEntities.Count - 1);
            if (_currentMode == SpectatorMode.PlayerFollow)
            {
                UpdateFollowTarget();
            }
        }
    }

    #endregion

    /// <summary>
    /// Set initial position for spectator camera.
    /// Called by SpectatorSpawner.
    /// </summary>
    public void SetInitialPosition(Vector3 position, Quaternion rotation)
    {
        transform.position = position;
        transform.rotation = rotation;

        Vector3 euler = rotation.eulerAngles;
        _rotationX = euler.y;
        _rotationY = euler.x;
    }
}
