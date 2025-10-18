using System;
using UnityEngine;

public class MovementSystem : ISystem
{
    private const float GRAVITY = 9.81f;
    private World _world;

    public void Initialize(World world)
    {
        _world = world;
        _world.Events.Subscribe<MovePressedInputEvent>(OnMoveInput);
    }

    public void Update(float dt)
    {
        foreach (var (entity, movement) in _world.Components.Query<MovementDataComponent>())
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

            ///////////////////////////////////////////////////////////

            // Event publishing
            _world.Events.Publish(
                new AnimationParameterEvent(entity, "isMoving", AnimationParameterType.Bool, movement.IsMoving)
            );

            if (movement.IsMoving)
            {
                Vector3 forward = Vector3.forward;
                Vector3 right = Vector3.right;

                float forwardDot = Vector3.Dot(movement.MoveDirection, forward);
                float rightDot = Vector3.Dot(movement.MoveDirection, right);

                _world.Events.Publish(
                    new AnimationParameterEvent(entity, "moveY", AnimationParameterType.Float, -forwardDot)
                );

                _world.Events.Publish(
                    new AnimationParameterEvent(entity, "moveX", AnimationParameterType.Float, rightDot)
                );
            }
            else
            {
                _world.Events.Publish(new AnimationParameterEvent(entity, "moveY", AnimationParameterType.Float, 0f));

                _world.Events.Publish(new AnimationParameterEvent(entity, "moveX", AnimationParameterType.Float, 0f));
            }
        }
    }

    private void OnMoveInput(MovePressedInputEvent @event)
    {
        if (_world.Components.TryGet(@event.Entity, out MovementDataComponent movement))
        {
            movement.InputDirection = @event.Input;
        }
    }

    public void FixedUpdate(float dt) { }

    public void Shutdown()
    {
        _world.Events.Unsubscribe<MovePressedInputEvent>(OnMoveInput);
    }
}
