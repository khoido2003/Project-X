using UnityEngine;

[CreateAssetMenu(fileName = "PlasmaShieldSkill", menuName = "Skills/Alex/PlasmaShieldSkill")]
public class PlasmaShieldSkill : SkillData
{
    public float defenseBoost = 20f;
    public float boostDuration = 5f;

    public override void Execute(GameObject owner, Vector3 targetPoint, Vector3 direction)
    {
        StatusEffectComponent statusEffect = owner.GetComponent<StatusEffectComponent>();

        if (statusEffect != null)
        {
            statusEffect.ApplyDefenseBoost(defenseBoost, boostDuration);
        }

        if (skillVfxEffectPrefab != null)
        {
            skillVfxEffectInstance = Instantiate(skillVfxEffectPrefab, owner.transform);
            skillVfxEffectInstance.transform.localPosition = Vector3.zero;
            skillVfxEffectInstance.Play();
            Destroy(skillVfxEffectInstance.gameObject, boostDuration);
        }
    }
}
