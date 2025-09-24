using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class InputManager : MonoBehaviour
{
    public static InputManager Instance { get; private set; }

    [SerializeField]
    private bool useNewInputSystem = true;

    [SerializeField]
    private PlayerInputAction inputActions;

    public event Action<Vector2> OnMove;
    public event Action OnAttackPressed;

    public event Action OnSkill1Pressed;
    public event Action OnSkill2Pressed;
    public event Action OnSkill3Pressed;

    public event Action OnSkill1Released;
    public event Action OnSkill2Released;
    public event Action OnSkill3Released;

    private Vector2 moveInput;

    private void Awake()
    {
        if (Instance != null)
        {
            Debug.LogError("More than one InputManager Instance!");
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (useNewInputSystem)
        {
            RegisterNewInputSystem();
        }
    }

    public void OnEnable()
    {
        if (useNewInputSystem)
        {
            inputActions.Player.Enable();
        }
    }

    public void Disable()
    {
        if (useNewInputSystem)
        {
            inputActions.Player.Disable();
        }
    }

    private void Update()
    {
        if (!useNewInputSystem)
        {
            HandleLegacyInputSystem();
        }
    }

    private void HandleLegacyInputSystem()
    {
        // Move Input (WASD)
        Vector2 legacyMove = new(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));

        // Avoid constantly update the vector input if the input still the same
        if (legacyMove != moveInput)
        {
            moveInput = legacyMove;
            OnMove?.Invoke(moveInput);
        }

        // Attack (mouse left)
        if (Input.GetMouseButtonDown(0))
        {
            OnAttackPressed?.Invoke();
        }

        // Skill 1 (mouse right)
        if (Input.GetKeyDown(KeyCode.Q))
        {
            OnSkill1Pressed?.Invoke();
        }

        // Skill 2 (space)
        if (Input.GetKeyDown(KeyCode.Space))
        {
            OnSkill2Pressed?.Invoke();
        }

        // Skill3 (E key)
        if (Input.GetKeyDown(KeyCode.E))
        {
            OnSkill3Pressed?.Invoke();
        }
    }

    private void RegisterNewInputSystem()
    {
        inputActions = new PlayerInputAction();

        inputActions.Player.Move.performed += (ctx) =>
        {
            moveInput = ctx.ReadValue<Vector2>().normalized;
            OnMove?.Invoke(moveInput);
        };

        inputActions.Player.Move.canceled += (ctx) =>
        {
            moveInput = Vector2.zero;
            OnMove?.Invoke(moveInput);
        };

        inputActions.Player.Attack.performed += (ctx) =>
        {
            OnAttackPressed?.Invoke();
        };

        inputActions.Player.Skill1.performed += (ctx) =>
        {
            OnSkill1Pressed?.Invoke();
        };

        inputActions.Player.Skill2.performed += (ctx) =>
        {
            OnSkill2Pressed?.Invoke();
        };

        inputActions.Player.Skill3.performed += (ctx) =>
        {
            OnSkill3Pressed?.Invoke();
        };

        inputActions.Player.Skill1.canceled += (ctx) =>
        {
            OnSkill1Released?.Invoke();
        };

        inputActions.Player.Skill2.canceled += (ctx) =>
        {
            OnSkill2Released?.Invoke();
        };

        inputActions.Player.Skill3.canceled += (ctx) =>
        {
            OnSkill3Released?.Invoke();
        };
    }

    public Vector2 GetMouseScreeenPosition()
    {
        if (useNewInputSystem)
        {
            return Mouse.current.position.ReadValue();
        }

        return Input.mousePosition;
    }
}
