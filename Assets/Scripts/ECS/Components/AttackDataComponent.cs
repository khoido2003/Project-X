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
    public bool IsPlayerControlled;
    public bool IsAttacking;
    public float LastAttackTime;
    public float AttackSpeedMultiplier = 1f;

    public Vector3 AttackDirection = Vector3.forward;

    public bool CanAttack(float baseCooldown)
    {
        float adjustedCooldown = baseCooldown / AttackSpeedMultiplier;
        return Time.time >= LastAttackTime + adjustedCooldown;
    }
}
