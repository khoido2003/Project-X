using UnityEngine;

public interface IStatusEffect
{
    public void ApplyStunt(float duration);
    public void ApplySlowDown(float slowPercentage, float duration);
    public void ApplyKnockback(Vector3 direction, float force);
    public bool IsStunned();
    public float GetSlowMultiplier();
}
