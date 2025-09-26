using UnityEngine;

public class StatusEffectComponent : MonoBehaviour, IStatusEffect
{
    private CharacterController controller;

    private float stuntEndTime = -Mathf.Infinity;
    private float slowEndTime = -Mathf.Infinity;

    // 1f = 100% speed, smaller than 1f make object slower like 50%, 80%
    private float slowMultiplier = 1f;

    private float defenseBoost = 0f;
    private float defenseBoostEndtime = -Mathf.Infinity;

    private void Start()
    {
        controller = GetComponent<CharacterController>();
    }

    public void ApplyKnockback(Vector3 direction, float force)
    {
        if (controller != null)
        {
            controller.Move(direction * force * Time.deltaTime);
        }
    }

    public void ApplySlowDown(float slowPercentage, float duration)
    {
        slowEndTime = Mathf.Max(slowEndTime, Time.time + duration);
        slowMultiplier = Mathf.Min(slowMultiplier, 1f - slowPercentage);
    }

    public void ApplyStunt(float duration)
    {
        stuntEndTime = Mathf.Max(stuntEndTime, Time.time + duration);
    }

    public bool IsStunned()
    {
        return Time.time < stuntEndTime;
    }

    public float GetSlowMultiplier()
    {
        if (Time.time > slowEndTime)
        {
            slowMultiplier = 1f;
        }
        return slowMultiplier;
    }

    public float GetDefenseBoost()
    {
        if (Time.time > defenseBoostEndtime)
        {
            defenseBoost = 0f;
        }

        return defenseBoost;
    }

    public void ApplyDefenseBoost(float boost, float duration)
    {
        defenseBoostEndtime = Mathf.Max(defenseBoostEndtime, Time.time + duration);

        defenseBoost = Mathf.Max(defenseBoost, boost);
    }
}
