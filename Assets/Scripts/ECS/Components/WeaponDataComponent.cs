using UnityEngine;

public class WeaponDataComponent
{
    [Header("Config")]
    public string WeaponName;
    public bool IsMelee = true;

    [Header("Stats")]
    public float AttackDamage;
    public float AttackCooldown;
    public float AttackRange;

    [Header("Visuals")]
    public GameObject WeaponPrefab;
    public GameObject ProjectilePrefab;
    public Vector3 SpawnPositionOffset = Vector3.zero;
    public Vector3 SpawnRotationOffset = Vector3.zero;
    public ParticleSystem HitImpactParticlePrefab;

    [Header("Animation & Audio")]
    public string AttackAnimationTrigger;
    public int TotalAttackAnimations;
    public AudioClip AttackSound;

    [Header("Runtime State")]
    public GameObject WeaponInstance;
    public Transform WeaponHolder;
}
