using UnityEngine;

public class WeaponDataComponent
{
    public string WeaponName;
    public AttackExecutionType ExecutionType = AttackExecutionType.Melee;

    public float BaseDamage = 10f;
    public float BaseCooldown = 1.2f;
    public float BaseRange = 2f;

    public ParticleSystem HitImpactParticlePrefab;

    public string AttackAnimationTrigger = "attack";
    public int TotalAttackAnimations = 1;

    public AudioClip AttackSound;

    public GameObject ProjectilePrefab;
    public float ProjectileSpeed = 15f;
    public float ProjectileLifetime = 5f;
    public Vector3 ProjectileSpawnOffset = new(0, 0, 0);

    public float AreaRadius = 2f;
    public float AreaDuration = 0f;
}
