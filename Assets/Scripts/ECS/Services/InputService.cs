using UnityEngine;

public class InputService : MonoBehaviour, IInputService
{
    [SerializeField]
    private LayerMask mouseLayerMask = ~0;

    private Vector2 _moveInput;
    private bool _attackPressed;
    private bool[] _skills = new bool[3];
    private Camera _mainCamera;

    private void Awake()
    {
        _mainCamera = Camera.main;
        if (_mainCamera == null)
        {
            Debug.LogError("Main Camera not found! Please tag your camera as MainCamera.");
        }
    }

    private void Start()
    {
        SubscribeInput();
    }

    private void SubscribeInput()
    {
        InputManager.Instance.OnMove += (v) => _moveInput = v;

        InputManager.Instance.OnAttackPressed += () => _attackPressed = true;
        InputManager.Instance.OnAttackReleased += () => _attackPressed = false;

        InputManager.Instance.OnSkill1Pressed += () => _skills[0] = true;
        InputManager.Instance.OnSkill2Pressed += () => _skills[1] = true;
        InputManager.Instance.OnSkill3Pressed += () => _skills[2] = true;

        InputManager.Instance.OnSkill1Released += () => _skills[0] = false;
        InputManager.Instance.OnSkill2Released += () => _skills[1] = false;
        InputManager.Instance.OnSkill3Released += () => _skills[2] = false;
    }

    public Vector2 GetMoveInput() => _moveInput;

    public bool IsAttackPressed()
    {
        bool pressed = _attackPressed;
        return pressed;
    }

    public bool IsSkillPressed(int index)
    {
        if (index < 1 || index > _skills.Length)
        {
            return false;
        }

        return _skills[index - 1];
    }

    public Vector3 GetMouseWorldPosition()
    {
        if (_mainCamera == null)
            return Vector3.zero;

        Ray ray = _mainCamera.ScreenPointToRay(InputManager.Instance.GetMouseScreeenPosition());
        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, mouseLayerMask))
        {
            return hit.point;
        }

        return Vector3.zero;
    }

    public Vector3 GetAimDirection(Vector3 origin)
    {
        Vector3 worldPos = GetMouseWorldPosition();
        if (worldPos == Vector3.zero)
            return Vector3.zero;

        Vector3 dir = (worldPos - origin).normalized;
        dir.y = 0f;

        return dir;
    }
}
