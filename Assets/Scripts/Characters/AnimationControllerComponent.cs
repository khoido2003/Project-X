using UnityEngine;

public class AnimationControllerComponent : MonoBehaviour
{
    private Animator animator;
    private Transform characterTransform;

    private void Start()
    {
        characterTransform = transform;
    }

    public void Bind(MovementComponent movement)
    {
        animator = GetComponentInChildren<Animator>();

        movement.OnVelocityChanged += MovementComponent_OnVelocicyChanged;
    }

    private void MovementComponent_OnVelocicyChanged(object sender, Vector3 moveDirection)
    {
        if (animator == null)
        {
            Debug.LogError("Missing animator component!");
            return;
        }

        bool isMoving = moveDirection.sqrMagnitude > 0.01f;

        animator.SetBool("isMoving", isMoving);

        if (isMoving)
        {
            float forwardDot = Vector3.Dot(moveDirection, characterTransform.forward);
            float rightDot = Vector3.Dot(moveDirection, characterTransform.right);

            animator.SetFloat("moveY", forwardDot);
            animator.SetFloat("moveX", rightDot);
        }
        else
        {
            animator.SetFloat("moveY", 0f);
            animator.SetFloat("moveX", 0f);
        }
    }
}
