using UnityEngine;

public class MovementSystem : ISystem
{
    private const float GRAVITY = 9.81f;
    private World _world;

    public void Initialize(World world)
    {
        _world = world;
    }

    public void Update(float dt)
    {
        foreach (var pair in _world.Components.Query<MovementData>())
        {
            EntityId entity = pair.Key;
            MovementData movement = pair.Value;

            // Apply Gravity
            if (movement.IsGrounded)
            {
                movement.VerticalVelocity = -0.5f;
            }
            else
            {
                movement.VerticalVelocity -= GRAVITY * dt;
            }

            // Stun effect
            if (movement.IsStunned)
            {
                movement.MoveDirection = Vector3.zero;
                continue;
            }

            // Calc move direction
            if (movement.InputDirection.sqrMagnitude > 0.01f)
            {
                Vector3 localInput = new(movement.InputDirection.x, 0f, movement.InputDirection.y);

                movement.MoveDirection = localInput.normalized * movement.ForwardMultiplier;
            }
            else
            {
                movement.MoveDirection = Vector3.zero;
            }
        }
    }

    public void FixedUpdate(float dt) { }

    public void Shutdown() { }
}
