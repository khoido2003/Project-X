using System;
using UnityEngine;

public class AnimationControllerComponent : MonoBehaviour
{
    private Animator animator;
    private Transform characterTransform;

    private static readonly int AttackIndex = Animator.StringToHash("attackIndex");

    private void Start()
    {
        characterTransform = transform;
    }

    public void Bind(MovementComponent movement, AttackComponent attack)
    {
        animator = GetComponentInChildren<Animator>();

        movement.OnVelocityChanged += MovementComponent_OnVelocicyChanged;
        attack.OnAttackTrigger += AttackComponent_OnAttackTrigger;
    }

    private void AttackComponent_OnAttackTrigger(object sender, WeaponData weaponData)
    {
        animator.SetTrigger(weaponData.attackAnimationTrigger);

        GetRandomAttackIndexAnimation(weaponData.totalAttackAnimations);
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

    private void GetRandomAttackIndexAnimation(int totalAttackAnimations)
    {
        int randomIndex = UnityEngine.Random.Range(0, totalAttackAnimations);

        animator.SetFloat(AttackIndex, randomIndex);
    }

    private void SwitchAnimationLayer(int layerIndex)
    {
        for (int i = 2; i < animator.layerCount; i++)
        {
            animator.SetLayerWeight(i, 0);
        }

        animator.SetLayerWeight(layerIndex, 1);
    }
}
