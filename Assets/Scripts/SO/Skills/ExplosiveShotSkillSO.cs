using UnityEngine;

[CreateAssetMenu(fileName = "ExplosiveShotSkill", menuName = "Skills/ExplosiveShotSkill")]
public class ExplosiveShotSkillSO : SkillDefinitionSO
{
    [Header("Explosive Shot Settings")]
    public float explosionRadius = 3f;
    public float explosionDamage = 40f;
    public GameObject projectilePrefab;
    public float projectileSpeed = 15f;
    public float projectileLifetime = 4f;
    public ParticleSystem explosionVfxPrefab;
}
