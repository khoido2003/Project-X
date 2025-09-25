using System.Collections;
using UnityEngine;

[CreateAssetMenu(fileName = "DashStrikeSkill", menuName = "Skills/Alex/DashStrikeSkill")]
public class DaskStrikeSkill : SkillData
{
    public float dashDistance = 5f;
    public float dashDuration = 0.3f;
    public float damage = 20f;
    public float attackRadius = 1f;

    public TrailRenderer dashVfxPrefab;

    private MonoBehaviour ownerMonoBehaviour;

    public override void Execute(GameObject owner, Vector3 targetPoint, Vector3 direction)
    {
        // Start dash coroutine
        ownerMonoBehaviour = owner.GetComponent<MonoBehaviour>();
        ownerMonoBehaviour.StartCoroutine(DashCoroutine(owner, targetPoint, direction));
    }

    private IEnumerator DashCoroutine(GameObject owner, Vector3 targetPoint, Vector3 direction)
    {
        CharacterController controller = owner.GetComponent<CharacterController>();

        if (controller == null)
        {
            yield break;
        }

        Vector3 startPos = owner.transform.position;
        float distanceToTarget = Vector3.Distance(startPos, targetPoint);
        float effectiveDash = Mathf.Min(dashDistance, distanceToTarget);
        Vector3 endPos = startPos + direction * effectiveDash;

        // Create and detach VFX
        TrailRenderer dashVfxInstance = null;
        if (dashVfxPrefab != null)
        {
            dashVfxInstance = Instantiate(dashVfxPrefab, owner.transform.position, owner.transform.rotation);
            dashVfxInstance.transform.SetParent(owner.transform);
        }

        float elapsed = 0f;
        while (elapsed < dashDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / dashDuration;

            Vector3 newPos = Vector3.Lerp(startPos, endPos, Mathf.SmoothStep(0, 1, t));
            controller.Move(newPos - owner.transform.position);

            // wait for next frame so trail can update
            yield return null;
        }

        if (dashVfxInstance != null)
        {
            dashVfxInstance.transform.SetParent(null);
            Destroy(dashVfxInstance.gameObject, 2f);
        }

        // Damage after dash ends
        Collider[] hits = Physics.OverlapSphere(endPos, attackRadius);
        foreach (Collider hit in hits)
        {
            HealthComponent enemyHealth = hit.GetComponent<HealthComponent>();
            if (enemyHealth != null && enemyHealth != owner.GetComponent<HealthComponent>())
            {
                enemyHealth.TakeDamage(damage);

                if (skillHitImpactEffectPrefab != null)
                {
                    skillHitImpactEffectInstance = Instantiate(
                        skillHitImpactEffectPrefab,
                        hit.ClosestPoint(ownerMonoBehaviour.transform.position),
                        Quaternion.identity
                    );

                    Destroy(skillHitImpactEffectInstance, 2f);
                }
            }
        }
    }
}
