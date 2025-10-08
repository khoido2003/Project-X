using System;
using UnityEngine;

public class AnimationView : EntityView
{
    private Animator animator;

    private void Awake()
    {
        animator = GetComponentInChildren<Animator>();
    }

    private void Start()
    {
        WorldInstance.Events.Subscribe<AnimationParameterEvent>(OnAnimationParameter);
    }

    private void Update()
    {
        if (WorldInstance == null)
            return;

        if (WorldInstance.Components.TryGet(EntityInstance, out AnimationDataComponent anim))
        {
            animator.SetBool("isMoving", anim.IsMoving);
            animator.SetFloat("moveX", anim.MoveX);
            animator.SetFloat("moveY", anim.MoveY);
        }
    }

    private void OnAnimationParameter(AnimationParameterEvent @event)
    {
        if (@event.Entity != EntityInstance)
        {
            return;
        }

        switch (@event.ParameterType)
        {
            case AnimationParameterType.Trigger:
                animator.SetTrigger(@event.ParameterName);
                break;

            case AnimationParameterType.Bool:
                animator.SetBool(@event.ParameterName, (bool)@event.Value);
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
        if (WorldInstance != null)
            WorldInstance.Events.Unsubscribe<AnimationParameterEvent>(OnAnimationParameter);
    }
}
