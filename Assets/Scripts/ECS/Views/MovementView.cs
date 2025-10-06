using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class MovementView : EntityView
{
    private CharacterController controller;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    private void Update()
    {
        World world = WorldInstance;
        EntityId entity = EntityInstance;

        if (world.Components.TryGet(entity, out MovementData movement))
        {
            movement.IsGrounded = controller.isGrounded;

            Vector3 moveDir = transform.right * movement.MoveDirection.x + transform.forward * movement.MoveDirection.z;

            Vector3 move = moveDir * movement.MoveSpeed + Vector3.up * movement.VerticalVelocity;

            controller.Move(move * Time.deltaTime);
        }
    }
}
