using UnityEngine;

[CreateAssetMenu(fileName = "SniperShotSkill", menuName = "Skills/SniperShotSkill")]
public class SniperShotSkillSO : SkillDefinitionSO
{
    [Header("Sniper Shot Settings")]
    public float chargeDuration = 1f;
    public float maxRange = 20f;
    public bool canPierce = true;
    public int maxPierceCount = 3;
    public GameObject projectilePrefab;
    public float projectileSpeed = 30f;
    public float projectileLifetime = 5f;
    public ParticleSystem chargeVfxPrefab;
}

