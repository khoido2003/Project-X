using UnityEngine;

public class MovementDataComponent
{
    [Header("Movement Settings")]
    public float MoveSpeed;
    public float ForwardMultiplier;

    [Header("Runtime State")]
    public Vector3 MoveDirection;
    public Vector2 InputDirection;
    public float VerticalVelocity;
    public bool IsPlayerControlled;
    public bool IsGrounded;
    public bool IsStunned;
    public bool IsMoving;
}
