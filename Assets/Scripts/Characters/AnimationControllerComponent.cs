using System.Collections.Generic;
using UnityEngine;

public class AnimationControllerComponent : MonoBehaviour
{
    private Animator animator;
    private Transform characterTransform;
    private List<IAnimationTrigger> triggerSource = new();

    private static readonly int AttackIndex = Animator.StringToHash("attackIndex");

    private void Start()
    {
        characterTransform = transform;
    }

    private void OnDestroy()
    {
        foreach (IAnimationTrigger source in triggerSource)
        {
            source.OnTriggerAnimation -= OnTriggerAnimation;
            source.OnSetFloatParameter -= OnSetFloatParameter;
            source.OnSetBoolParameter -= OnSetBoolParameter;
        }

        triggerSource.Clear();
    }

    public void Bind(IEnumerable<IAnimationTrigger> sources)
    {
        animator = GetComponentInChildren<Animator>();

        if (animator == null)
        {
            Debug.LogError($"Missing Animator component on {gameObject.name}");

            return;
        }

        triggerSource.Clear();

        foreach (IAnimationTrigger source in sources)
        {
            triggerSource.Add(source);
            source.OnTriggerAnimation += OnTriggerAnimation;
            source.OnSetFloatParameter += OnSetFloatParameter;
            source.OnSetBoolParameter += OnSetBoolParameter;
        }
    }

    #region ANIMATION_EVENTS

    private void OnTriggerAnimation(string triggerName)
    {
        animator.SetTrigger(triggerName);
    }

    private void OnSetFloatParameter(string parameterName, float value)
    {
        animator.SetFloat(parameterName, value);
    }

    private void OnSetBoolParameter(string parameterName, bool value)
    {
        animator.SetBool(parameterName, value);
    }

    #endregion


    public void SwitchAnimationLayer(int layerIndex)
    {
        for (int i = 1; i < animator.layerCount; i++)
        {
            animator.SetLayerWeight(i, -1);
        }

        animator.SetLayerWeight(layerIndex, 0);
    }
}
