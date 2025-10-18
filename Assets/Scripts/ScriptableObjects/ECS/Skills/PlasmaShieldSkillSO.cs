using UnityEngine;

[CreateAssetMenu(fileName = "PlasmaShieldSkill", menuName = "Skills/PlasmaShieldSkill")]
public class PlasmaShieldSkillSO : SkillDefinitionSO
{
    public float defenseBoost = 20f;
    public float boostDuration = 5f;
    public GameObject shieldPrefab;
}
