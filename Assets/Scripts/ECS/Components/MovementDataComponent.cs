using UnityEngine;

public class MovementDataComponent
{
    public float MoveSpeed;
    public float ForwardMultiplier;

    public Vector3 MoveDirection;
    public Vector2 InputDirection;
    public float VerticalVelocity;
    public bool IsPlayerControlled;
    public bool IsGrounded;
    public bool IsStunned;
    public bool IsMoving;

    /// <summary>
    /// Computed velocity based on movement direction and speed.
    /// Used for target prediction in enemy AI.
    /// </summary>
    public Vector3 Velocity => IsMoving ? MoveDirection.normalized * MoveSpeed : Vector3.zero;
}
