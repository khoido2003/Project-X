using UnityEngine;

[CreateAssetMenu(fileName = "DashStrikeSkill", menuName = "Skills/DashStrikeSkill")]
public class DaskStrikeSkill : SkillData
{
    public float dashDistance = 5f;
    public float dashDuration = 0.3f;
    public float damage = 20f;
    public float attackRadius = 1f;

    public override void Execute(GameObject owner, Vector3 targetPoint, Vector3 direction)
    {
        CharacterController controller = owner.GetComponent<CharacterController>();

        if (controller != null)
        {
            Vector3 startPos = owner.transform.position;

            float distanceToTarget = Vector3.Distance(startPos, targetPoint);

            float effectiveDash = Mathf.Min(dashDistance, distanceToTarget);

            Vector3 endPos = startPos + direction * effectiveDash;

            // Dash time
            float elapsed = 0f;
            while (elapsed < dashDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / dashDuration;

                controller.Move((endPos - startPos) * (Time.deltaTime / dashDuration));
            }

            // Deal damage in radius
            Collider[] hits = Physics.OverlapSphere(endPos, attackRadius);

            foreach (Collider hit in hits)
            {
                HealthComponent enemyHealth = hit.GetComponent<HealthComponent>();

                if (enemyHealth != null && enemyHealth != owner.GetComponent<HealthComponent>())
                {
                    enemyHealth.TakeDamage(damage);
                }
            }
        }
    }
}
