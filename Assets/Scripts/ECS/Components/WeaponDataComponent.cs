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
