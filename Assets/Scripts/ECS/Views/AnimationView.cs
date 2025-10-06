using UnityEngine;

public class AnimationView : EntityView
{
    private Animator animator;

    private void Awake()
    {
        animator = GetComponentInChildren<Animator>();
    }

    private void Update()
    {
        if (WorldInstance.Components.TryGet(EntityInstance, out AnimationData animation))
        {
            animator.SetBool("isMoving", animation.IsMoving);
            animator.SetFloat("moveX", animation.MoveX);
            animator.SetFloat("moveY", animation.MoveY);
        }
    }
}
