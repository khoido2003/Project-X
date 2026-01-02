using UnityEngine;

public class HealthDataComponent
{
    [Header("Health Stats")]
    public float MaxHealth;
    public float CurrentHealth;
    public bool IsDead = false;
    
    [Header("Status Flags")]
    public bool IsUntargetable = false;
    public bool IsInvincible = false;
}
