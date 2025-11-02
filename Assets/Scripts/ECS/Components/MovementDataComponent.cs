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
}
