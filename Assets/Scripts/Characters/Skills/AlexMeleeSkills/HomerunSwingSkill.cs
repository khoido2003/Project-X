using System.Collections;
using UnityEngine;

[CreateAssetMenu(fileName = "HomerunSwingSkill", menuName = "Skills/Alex/HomerunSwingSkill")]
public class HomerunSwingSkill : SkillData
{
    public float chargeDuration = 0.5f;
    public float damage = 30f;
    public float attackRadius = 2f;
    public float knockbakcForce = 8f;
    public float stuntDuration = 1.5f;

    private MonoBehaviour ownerMonoBehaviour;
    private HealthComponent currentHealth;
    private WeaponComponent weaponComponent;
    private StatusEffectComponent statusEffect;

    private Vector3 lastHitPoint;

    public override void Execute(GameObject owner, Vector3 targetPoint, Vector3 direction)
    {
        currentHealth = owner.GetComponent<HealthComponent>();
        statusEffect = owner.GetComponent<StatusEffectComponent>();

        ownerMonoBehaviour = owner.GetComponent<MonoBehaviour>();
        ownerMonoBehaviour.StartCoroutine(SwingCoroutine(owner, targetPoint, direction));
    }

    private IEnumerator SwingCoroutine(GameObject owner, Vector3 targetPoint, Vector3 direction)
    {
        yield return new WaitForSeconds(chargeDuration);

        Collider[] hits = Physics.OverlapSphere(targetPoint, attackRadius);
        foreach (Collider hit in hits)
        {
            HealthComponent enemyHealth = hit.GetComponent<HealthComponent>();

            lastHitPoint = Vector3.zero;

            if (enemyHealth != null && enemyHealth != owner.GetComponent<HealthComponent>())
            {
                // Save hit point for VFX effect
                lastHitPoint = hit.ClosestPoint(owner.transform.position);

                enemyHealth.TakeDamage(damage);

                // Stop the weapon vfx effect
                OnWeaponVfxEffectStop(owner);

                if (statusEffect != null)
                {
                    statusEffect.ApplyKnockback(direction, knockbakcForce);
                    statusEffect.ApplyStunt(stuntDuration);
                }
            }
        }
    }

    public override void OnWeaponVfxEffectStart(GameObject owner)
    {
        WeaponComponent weapon = owner.GetComponent<WeaponComponent>();

        if (weapon == null || skillVfxEffectPrefab == null)
        {
            return;
        }

        Transform vfxEffectSocketTransform = weapon.GetSocket(WeaponVfxEffectSocketName.CHARGE);

        if (vfxEffectSocketTransform == null)
        {
            return;
        }

        skillVfxEffectInstance = Instantiate(
            skillVfxEffectPrefab,
            vfxEffectSocketTransform.position,
            Quaternion.identity,
            vfxEffectSocketTransform
        );
    }

    public override void OnWeaponVfxEffectStop(GameObject owner)
    {
        if (skillVfxEffectInstance != null)
        {
            skillVfxEffectInstance.Stop();
            Destroy(skillVfxEffectInstance.gameObject, 1f);
            skillVfxEffectInstance = null;
        }
    }

    public override void OnTriggerSkillVfxEffect()
    {
        if (ownerMonoBehaviour == null || ownerMonoBehaviour.gameObject == null)
            return;

        // No enemy found
        if (lastHitPoint == Vector3.zero)
        {
            OnWeaponVfxEffectStop(ownerMonoBehaviour.gameObject);
            return;
        }

        Vector3 spawnPos = ownerMonoBehaviour.transform.position;

        skillHitImpactEffectInstance = Instantiate(
            skillHitImpactEffectPrefab,
            lastHitPoint,
            Quaternion.identity,
            ownerMonoBehaviour.transform
        );

        Destroy(skillHitImpactEffectInstance.gameObject, 2f);
        OnWeaponVfxEffectStop(ownerMonoBehaviour.gameObject);
    }
}
