using UnityEngine;

public class InputService : MonoBehaviour, IInputService
{
    [SerializeField]
    private LayerMask mouseLayerMask;

    private Vector2 _moveInput;
    private bool _attackHeld;
    private bool _attackDown;
    private bool _attackUp;

    private bool[] _skillHeld = new bool[3];
    private bool[] _skillDown = new bool[3];
    private bool[] _skillUp = new bool[3];

    private Camera _mainCamera;

    private void Awake()
    {
        _mainCamera = Camera.main;
        if (_mainCamera == null)
        {
            Debug.LogError("Main Camera not found! Please tag camera as MainCamera.");
        }
    }

    private void Start()
    {
        SubscribeInput();
    }

    private void LateUpdate()
    {
        _attackDown = false;
        _attackUp = false;

        for (int i = 0; i < 3; i++)
        {
            _skillDown[i] = false;
            _skillUp[i] = false;
        }
    }

    private void SubscribeInput()
    {
        var input = InputManager.Instance;

        input.OnMove += (v) => _moveInput = v;

        // Attack
        input.OnAttackPressed += () =>
        {
            _attackHeld = true;
            _attackDown = true;
        };
        input.OnAttackReleased += () =>
        {
            _attackHeld = false;
            _attackUp = true;
        };

        // Skills
        input.OnSkill1Pressed += () =>
        {
            _skillHeld[0] = true;
            _skillDown[0] = true;
        };
        input.OnSkill2Pressed += () =>
        {
            _skillHeld[1] = true;
            _skillDown[1] = true;
        };
        input.OnSkill3Pressed += () =>
        {
            _skillHeld[2] = true;
            _skillDown[2] = true;
        };

        input.OnSkill1Released += () =>
        {
            _skillHeld[0] = false;
            _skillUp[0] = true;
        };
        input.OnSkill2Released += () =>
        {
            _skillHeld[1] = false;
            _skillUp[1] = true;
        };
        input.OnSkill3Released += () =>
        {
            _skillHeld[2] = false;
            _skillUp[2] = true;
        };
    }

    public Vector2 GetMoveInput() => _moveInput;

    public bool IsLeftMouseDown() => _attackDown;

    public bool IsLeftMouseHeld() => _attackHeld;

    public bool IsLeftMouseUp() => _attackUp;

    public bool IsSkillDown(int index) => index >= 1 && index <= 3 && _skillDown[index - 1];

    public bool IsSkillUp(int index) => index >= 1 && index <= 3 && _skillUp[index - 1];

    public Vector3 GetMouseWorldPosition()
    {
        if (_mainCamera == null)
        {
            return Vector3.zero;
        }

        Ray ray = _mainCamera.ScreenPointToRay(InputManager.Instance.GetMouseScreeenPosition());

        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, mouseLayerMask))
        {
            return hit.point;
        }

        return Vector3.zero;
    }
}
