using UnityEngine;

[CreateAssetMenu(fileName = "RapidFireSkill", menuName = "Skills/RapidFireSkill")]
public class RapidFireSkillSO : SkillDefinitionSO
{
    [Header("Rapid Fire Settings")]
    public int projectileCount = 5;
    public float timeBetweenShots = 0.1f;
    public float spreadAngle = 5f;
    public GameObject projectilePrefab;
    public float projectileSpeed = 20f;
    public float projectileLifetime = 3f;
}

