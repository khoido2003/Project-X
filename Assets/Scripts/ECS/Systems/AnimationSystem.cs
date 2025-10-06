using UnityEngine;

public class AnimationSystem : ISystem
{
    private World _world;

    public void Initialize(World world)
    {
        _world = world;

        SubscribeEvents();
    }

    public void Update(float dt) { }

    public void FixedUpdate(float dt) { }

    public void Shutdown()
    {
        _world.Events.Unsubscribe<MovementStartedEvent>(OnMovementStart);
        _world.Events.Unsubscribe<MovementStoppedEvent>(OnMovementStop);
        _world.Events.Unsubscribe<MovementDirectionChangedEvent>(OnDirectionChanged);
    }

    private void SubscribeEvents()
    {
        _world.Events.Subscribe<MovementStartedEvent>(OnMovementStart);
        _world.Events.Subscribe<MovementStoppedEvent>(OnMovementStop);
        _world.Events.Subscribe<MovementDirectionChangedEvent>(OnDirectionChanged);
    }

    private void OnDirectionChanged(MovementDirectionChangedEvent @event)
    {
        if (!_world.Components.TryGet(@event.Entity, out AnimationData anim))
        {
            return;
        }

        if (!_world.Components.TryGet(@event.Entity, out MovementData movement))
        {
            return;
        }

        Vector3 forward = Vector3.forward * movement.ForwardMultiplier;
        Vector3 right = Vector3.right;

        anim.MoveY = -Vector3.Dot(@event.Direction, forward);
        anim.MoveX = Vector3.Dot(@event.Direction, right);
    }

    private void OnMovementStop(MovementStoppedEvent @event)
    {
        if (_world.Components.TryGet(@event.Entity, out AnimationData animation))
        {
            animation.IsMoving = false;
            animation.MoveX = 0f;
            animation.MoveY = 0f;
        }
    }

    private void OnMovementStart(MovementStartedEvent @event)
    {
        if (_world.Components.TryGet(@event.Entity, out AnimationData animation))
        {
            animation.IsMoving = true;
        }
    }
}
