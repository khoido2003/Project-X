using UnityEngine;

public enum AttackExecutionType
{
    Melee,
    Projectile,
    Area,
    Beam,
    Custom,
}

public class AttackDataComponent
{
    [Header("Control & State")]
    public bool IsPlayerControlled;
    public bool IsAttacking;
    public float LastAttackTime;
    public float AttackSpeedMultiplier = 1f;

    [Header("Attack Direction")]
    public Vector3 AttackDirection = Vector3.forward;

    // Derived values — computed each frame
    public bool CanAttack(float baseCooldown)
    {
        float adjustedCooldown = baseCooldown / AttackSpeedMultiplier;
        return Time.time >= LastAttackTime + adjustedCooldown;
    }
}
