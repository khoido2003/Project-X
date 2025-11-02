using UnityEngine;

[System.Serializable]
public class AttackDefinition
{
    [Header("General")]
    public string attackName;
    public AttackExecutionType executionType;

    [Header("Stats")]
    public float damage = 10f;
    public float cooldown = 1.5f;
    public float range = 2f;

    [Header("Animation")]
    public string animationTrigger = "attack";
    public int totalAnimations = 1;

    [Header("Visuals & Audio")]
    public ParticleSystem hitImpactVFX;
    public AudioClip attackSound;

    [Header("Projectile (Optional)")]
    public GameObject projectilePrefab;
    public float projectileSpeed = 15f;
    public float projectileLifetime = 5f;
    public Vector3 projectileSpawnOffset = new(0, 0, 0f);

    [Header("Area (Optional)")]
    public float areaRadius = 2f;
    public float areaDuration = 0f;
}
