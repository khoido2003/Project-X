using System.Collections.Generic;
using UnityEngine;

public class MurderKittenAttackComponent : AttackComponent
{
    protected override void ExcuteAttack(Vector3 direction)
    {
        if (weaponData == null)
        {
            Debug.LogWarning("MurderKitten: No weapon data available for attack!");
            return;
        }

        // Check for hits within attack range from character position
        Collider[] hits = Physics.OverlapSphere(transform.position, weaponData.attackRange);

        if (hits.Length > 0)
        {
            HashSet<HealthComponent> damaged = new();
            foreach (Collider hit in hits)
            {
                HealthComponent enemyHealth = hit.GetComponent<HealthComponent>();

                if (
                    enemyHealth != null
                    && enemyHealth != GetComponent<HealthComponent>()
                    && !damaged.Contains(enemyHealth)
                )
                {
                    damaged.Add(enemyHealth);
                    enemyHealth.TakeDamage(weaponData.attackDamage);

                    Debug.Log($"MurderKitten dealt {weaponData.attackDamage} damage to {hit.gameObject.name}");

                    // TODO: spawn hit effect
                }
            }
        }
    }
}
