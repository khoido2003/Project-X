using System;
using UnityEngine;

public class MovementComponent : MonoBehaviour
{
    private const float GRAVITY = 9.81f;
    private const float ROTATION_SPEED = 650f;

    private Vector3 moveDirection = Vector3.zero;
    private float moveSpeed;
    private float verticalVelocity = -0.5f;

    private CharacterController controller;
    private Character character;
    private MouseWorldPosition mouseWorldPosition;

    public event EventHandler<Vector3> OnVelocityChanged;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        character = GetComponent<Character>();
        mouseWorldPosition = GetComponent<MouseWorldPosition>();
    }

    public void Initialize(StatsData statsData, bool isPlayer)
    {
        moveSpeed = statsData.moveSpeed;

        if (isPlayer && InputManager.Instance != null)
        {
            InputManager.Instance.OnMove += SetMoveDirection;
        }

        if (isPlayer && mouseWorldPosition != null)
        {
            mouseWorldPosition.OnDirectionToMouseChanged += UpdateRotation;
        }
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
            InputManager.Instance.OnMove -= SetMoveDirection;
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

    private void Move()
    {
        if (controller != null)
        {
            Vector3 move = moveDirection * moveSpeed + Vector3.up * verticalVelocity;

            // Notice the animator to trigger animation
            OnVelocityChanged?.Invoke(this, moveDirection);

            controller.Move(move * Time.deltaTime);
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

    private void SetMoveDirection(Vector2 input)
    {
        if (input.sqrMagnitude < 0.01f)
        {
            moveDirection = Vector3.zero;
            return;
        }

        Vector3 forward = transform.forward;
        Vector3 right = transform.right;

        forward.y = 0f;
        right.y = 0f;

        forward.Normalize();
        right.Normalize();

        moveDirection = (right * input.x + forward * input.y).normalized;
    }
}
