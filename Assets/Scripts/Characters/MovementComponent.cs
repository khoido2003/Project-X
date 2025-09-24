using System;
using UnityEngine;

public class MovementComponent : MonoBehaviour, IAnimationTrigger
{
    private const float GRAVITY = 9.81f;
    private const float ROTATION_SPEED = 650f;

    private Vector3 moveDirection = Vector3.zero;
    private float moveSpeed;
    private float verticalVelocity = -0.5f;
    private float forwardMultiplier;

    private CharacterController controller;
    private Character character;
    private MouseWorldPosition mouseWorldPosition;

    public event Action<string> OnTriggerAnimation;
    public event Action<string, float> OnSetFloatParameter;
    public event Action<string, bool> OnSetBoolParameter;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        character = GetComponent<Character>();
        mouseWorldPosition = GetComponent<MouseWorldPosition>();
    }

    public void Initialize(StatsData statsData, bool isPlayer)
    {
        moveSpeed = statsData.moveSpeed;

        // Character movememt
        if (isPlayer && InputManager.Instance != null)
        {
            InputManager.Instance.OnMove += InputManager_OnMove;
        }

        // Character look around
        if (isPlayer && mouseWorldPosition != null)
        {
            mouseWorldPosition.OnDirectionToMouseChanged += UpdateRotation;
        }

        forwardMultiplier = GetComponent<Character>()?.Data?.forwardDirectionMultiplier ?? 1f;
    }

    private void Update()
    {
        ApplyGravity();
        Move();
    }

    private void OnDestroy()
    {
        if (InputManager.Instance != null)
        {
            InputManager.Instance.OnMove -= InputManager_OnMove;
        }
    }

    private void UpdateRotation(Vector3 directionToMouse)
    {
        if (character != null && directionToMouse.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(directionToMouse);

            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                ROTATION_SPEED * Time.deltaTime
            );
        }
    }

    private void ApplyGravity()
    {
        if (controller.isGrounded)
        {
            verticalVelocity = -0.5f;
        }
        else
        {
            verticalVelocity -= GRAVITY * Time.fixedDeltaTime;
        }
    }

    private void Move()
    {
        if (controller != null)
        {
            Vector3 move = moveDirection * moveSpeed + Vector3.up * verticalVelocity;

            // TRIGGER ANIMATION HERE
            bool isMoving = moveDirection.sqrMagnitude > 0.01f;

            OnSetBoolParameter?.Invoke("isMoving", isMoving);

            if (isMoving)
            {
                Vector3 forward = transform.forward * forwardMultiplier;
                Vector3 right = transform.right;

                float forwardDot = Vector3.Dot(moveDirection, forward);
                float rightDot = Vector3.Dot(moveDirection, right);

                OnSetFloatParameter?.Invoke("moveY", -forwardDot);
                OnSetFloatParameter?.Invoke("moveX", rightDot);
            }
            else
            {
                OnSetFloatParameter?.Invoke("moveY", 0f);
                OnSetFloatParameter?.Invoke("moveX", 0f);
            }

            controller.Move(move * Time.deltaTime);
        }
    }

    private void InputManager_OnMove(Vector2 input)
    {
        if (input.sqrMagnitude < 0.01f)
        {
            moveDirection = Vector3.zero;
            return;
        }

        Vector3 localInput = new Vector3(input.x, 0f, input.y);

        // Convert local input to world-space relative to the character's transform.
        Vector3 worldMove = transform.right * localInput.x + transform.forward * localInput.z;

        moveDirection = worldMove.normalized * forwardMultiplier;
    }

    // private void OnDrawGizmos()
    // {
    //     Gizmos.color = Color.blue;
    //     Gizmos.DrawLine(transform.position, transform.position + transform.forward * 2);
    // }
}
