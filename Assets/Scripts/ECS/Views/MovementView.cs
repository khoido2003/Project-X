using Unity.Netcode;
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
        if (!NetworkManager.Singleton.IsServer)
        {
            return;
        }

        World world = WorldInstance;
        EntityId entity = EntityInstance;

        if (world.Components.TryGet(entity, out MovementDataComponent movement))
        {
            movement.IsGrounded = controller.isGrounded;

            // World-space movement: WASD moves in fixed world directions
            // Character rotation (facing mouse) is independent of movement
            Vector3 moveDir = new Vector3(-movement.MoveDirection.x, 0f, -movement.MoveDirection.z);

            Vector3 move = moveDir * movement.MoveSpeed + Vector3.up * movement.VerticalVelocity;

            controller.Move(move * Time.deltaTime);
        }
    }
}
