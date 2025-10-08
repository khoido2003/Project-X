using System;
using UnityEngine;

public class AnimationSystem : ISystem
{
    private World _world;

    public void Initialize(World world)
    {
        _world = world;
        _world.Events.Subscribe<AnimationParameterEvent>(OnAnimationParameter);
    }

    private void OnAnimationParameter(AnimationParameterEvent @event) { }

    public void Update(float dt) { }

    public void FixedUpdate(float dt) { }

    public void Shutdown()
    {
        _world.Events.Unsubscribe<AnimationParameterEvent>(OnAnimationParameter);
    }
}
