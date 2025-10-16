using UnityEngine;

[CreateAssetMenu(fileName = "DashStrikeSkill", menuName = "Skills/DashStrikeSkillSO")]
public class DashStrikeSkillSO : SkillDefinitionSO
{
    public float dashDistance = 5f;
    public float dashDuration = 0.3f;
    public float attackRadius = 1f;
    public TrailRenderer dashTrailPrefab;
}
