using UnityEngine;

public class AnimationEventRelay : MonoBehaviour
{
    private AttackComponent attackComponent;

    private void Start()
    {
        attackComponent = GetComponentInParent<AttackComponent>();
    }

    public void OnAttackHit()
    {
        attackComponent?.PerformHit();
    }
}
