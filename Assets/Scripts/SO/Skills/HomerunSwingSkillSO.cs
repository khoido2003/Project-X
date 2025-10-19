using UnityEngine;

[CreateAssetMenu(fileName = "HomerunSwingSkill", menuName = "Skills/HomerunSwingSkill")]
public class HomerunSwingSkillSO : SkillDefinitionSO
{
    public float chargeDuration = 0.5f;
    public float attackRadius = 2f;
    public float knockbackForce = 8f;
    public float stunDuration = 1.5f;
    public float coneAngle = 120f;
    public ParticleSystem swingVfxPrefab;
}
