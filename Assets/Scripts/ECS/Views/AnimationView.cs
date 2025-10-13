using System;
using UnityEngine;

public class AnimationView : EntityView
{
    private Animator animator;
    private bool _isInitialized;

    private void Awake()
    {
        animator = GetComponentInChildren<Animator>();
    }

    public override void Bind(World world, EntityId entity)
    {
        base.Bind(world, entity);

        WorldInstance.Events.Subscribe<AnimationParameterEvent>(OnAnimationParameter);
        _isInitialized = true;
    }

    private void OnAnimationParameter(AnimationParameterEvent @event)
    {
        if (!_isInitialized || @event.Entity != EntityInstance)
            return;

        switch (@event.ParameterType)
        {
            case AnimationParameterType.Trigger:
                animator.SetTrigger(@event.ParameterName);
                break;
            case AnimationParameterType.Bool:
                animator.SetBool(@event.ParameterName, Convert.ToBoolean(@event.Value));
                break;
            case AnimationParameterType.Float:
                animator.SetFloat(@event.ParameterName, Convert.ToSingle(@event.Value));
                break;
            case AnimationParameterType.Int:
                animator.SetInteger(@event.ParameterName, Convert.ToInt32(@event.Value));
                break;
        }
    }

    private void OnDestroy()
    {
        if (_isInitialized && WorldInstance != null)
            WorldInstance.Events.Unsubscribe<AnimationParameterEvent>(OnAnimationParameter);
    }
}
