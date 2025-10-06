using UnityEngine;

public class AnimationSystem : ISystem
{
    private World _world;

    public void Initialize(World world)
    {
        _world = world;
    }

    public void Update(float dt)
    {
        foreach (var (entity, movement) in _world.Components.Query<MovementData>())
        {
            if (!_world.Components.TryGet(entity, out AnimationData animation))
            {
                continue;
            }

            bool isMoving = movement.MoveDirection.sqrMagnitude > 0.01f;

            animation.IsMoving = isMoving;

            if (isMoving)
            {
                Vector3 forward = Vector3.forward * movement.ForwardMultiplier;
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

    public void Shutdown() { }
}
