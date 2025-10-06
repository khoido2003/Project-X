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

    // allow detect 120 degree damage in hit direction
    public float coneAngle = 120f;

    private MonoBehaviour ownerMonoBehaviour;
    private HealthComponent currentHealth;
    private WeaponComponent weaponComponent;
    private StatusEffectComponent statusEffect;
    private HealthComponent enemyHealth;

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

        Transform ownerTransform = owner.transform;
        lastHitPoint = Vector3.zero;
        enemyHealth = null;

        Collider[] hits = Physics.OverlapSphere(ownerTransform.position, attackRadius);
        foreach (Collider hit in hits)
        {
            HealthComponent hitHealth = hit.GetComponent<HealthComponent>();
            if (hitHealth == null || hitHealth == currentHealth)
                continue;

            // Vector from owner to this enemy
            Vector3 toEnemy = hit.transform.position - ownerTransform.position;
            float distanceToHit = toEnemy.magnitude;
            if (distanceToHit < 0.001f)
            {
                continue;
            }

            // Ignore vertical differences
            toEnemy.y = 0f;
            direction.y = 0f;

            Vector3 dirToEnemy = toEnemy.normalized;
            Vector3 forward = direction.sqrMagnitude > 0 ? direction.normalized : ownerTransform.forward;

            float angleToHit = Vector3.Angle(forward, dirToEnemy);

            if (angleToHit <= coneAngle * 0.5f)
            {
                lastHitPoint = hit.ClosestPoint(ownerTransform.position);
                enemyHealth = hitHealth;

                OnWeaponVfxEffectStop(owner);

                if (statusEffect != null)
                {
                    statusEffect.ApplyKnockback(forward, knockbakcForce);
                    statusEffect.ApplyStunt(stuntDuration);
                }

                // stop at first enemy, remove if multi-target
                break;
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

        if (enemyHealth != null)
        {
            enemyHealth.TakeDamage(damage);
            enemyHealth = null;
        }

        Destroy(skillHitImpactEffectInstance.gameObject, 2f);
        OnWeaponVfxEffectStop(ownerMonoBehaviour.gameObject);
    }
}
