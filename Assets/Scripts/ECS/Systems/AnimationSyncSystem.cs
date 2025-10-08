using UnityEngine;

public class AnimationSyncSystem : ISystem
{
    private World _world;

    public void Initialize(World world)
    {
        _world = world;
    }

    public void Shutdown() { }

    public void Update(float dt)
    {
        foreach (
            var (entity, movement, animation) in _world.Components.Query<
                MovementDataComponent,
                AnimationDataComponent
            >()
        )
        {
            animation.IsMoving = movement.IsMoving;

            if (movement.IsMoving)
            {
                Vector3 forward = Vector3.forward;
                Vector3 right = Vector3.right;

                animation.MoveY = -Vector3.Dot(movement.MoveDirection, forward);
                animation.MoveX = Vector3.Dot(movement.MoveDirection, right);
            }
            else
            {
                animation.MoveX = 0f;
                animation.MoveY = 0f;
            }
        }
    }

    public void FixedUpdate(float dt) { }
}
