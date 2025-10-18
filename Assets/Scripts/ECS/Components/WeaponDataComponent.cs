using UnityEngine;

public class WeaponDataComponent
{
    [Header("Config")]
    public string WeaponName;
    public AttackExecutionType ExecutionType;

    [Header("Stats")]
    public float BaseDamage;
    public float BaseCooldown;
    public float BaseRange;

    [Header("Visuals")]
    public ParticleSystem HitImpactParticlePrefab;

    [Header("Animation & Audio")]
    public string AttackAnimationTrigger;
    public int TotalAttackAnimations;
    public AudioClip AttackSound;
}
