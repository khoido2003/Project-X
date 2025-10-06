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
        foreach (var (entity, movement) in _world.Components.Query<MovementData>())
        {
            Vector3 previousDirection = movement.MoveDirection;

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
                movement.IsMoving = false;
                movement.MoveDirection = Vector3.zero;
                continue;
            }

            // Calc move direction
            if (movement.InputDirection.sqrMagnitude > 0.01f)
            {
                Vector3 localInput = new(movement.InputDirection.x, 0f, movement.InputDirection.y);

                movement.MoveDirection = localInput.normalized * movement.ForwardMultiplier;

                movement.IsMoving = true;
            }
            else
            {
                movement.MoveDirection = Vector3.zero;
                movement.IsMoving = false;
            }

            // Event publishing
            if (movement.IsMoving)
            {
                _world.Events.Publish(new MovementStartedEvent(entity));
            }
            if (!movement.IsMoving)
            {
                _world.Events.Publish(new MovementStoppedEvent(entity));
            }

            if ((movement.MoveDirection - previousDirection).sqrMagnitude > 0.001f)
            {
                _world.Events.Publish(new MovementDirectionChangedEvent(entity, movement.MoveDirection));
            }
        }
    }

    public void FixedUpdate(float dt) { }

    public void Shutdown() { }
}
