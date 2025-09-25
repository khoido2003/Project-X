using UnityEngine;

public class PowerSwingSkill : SkillData
{
    public float damage = 30f;
    public float attackRadius = 2f;
    public float knockbackForce = 5f;

    public override void Execute(GameObject owner, Vector3 targetPoint, Vector3 direction)
    {
        Collider[] hits = Physics.OverlapSphere(targetPoint, attackRadius);
        HealthComponent currentHealth = owner.GetComponent<HealthComponent>();

        foreach (var hit in hits)
        {
            HealthComponent enemyHealth = hit.GetComponent<HealthComponent>();

            if (enemyHealth != null && enemyHealth != currentHealth)
            {
                enemyHealth.TakeDamage(damage);
            }
        }
    }
}
