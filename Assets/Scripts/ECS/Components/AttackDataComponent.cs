using UnityEngine;

public class AttackDataComponent
{
    [Header("Attack State")]
    public bool IsPlayerControlled;
    public bool IsAttacking;
    public float LastAttackTime;
    public Vector3 AttackDirection;
}
