using System;
using System.Collections.Generic;
using UnityEngine;

public class AttackComponent : MonoBehaviour
{
    private WeaponData weaponData;
    private HealthComponent currentHealthComponent;

    private float lastAttackTime = -Mathf.Infinity;
    private bool isPlayer;

    private Vector3 hitDirection;

    public event EventHandler<WeaponData> OnAttackTrigger;

    private void Awake()
    {
        currentHealthComponent = GetComponent<HealthComponent>();
    }

    public void Initialize(WeaponData data, bool isPlayerControlled)
    {
        weaponData = data;
        isPlayer = isPlayerControlled;

        if (!isPlayer)
        {
            return;
        }

        InputManager.Instance.OnAttackPressed += InputManager_OnAttackPressed;
    }

    private void InputManager_OnAttackPressed()
    {
        if (weaponData == null || Time.time < lastAttackTime + weaponData.attackCooldown)
        {
            return;
        }

        hitDirection = transform.forward;

        // Trigger animation
        OnAttackTrigger?.Invoke(this, weaponData);

        lastAttackTime = Time.time;
    }

    public void PerformHit()
    {
        ExcuteAttack(hitDirection);
    }

    private void ExcuteAttack(Vector3 direction)
    {
        // TODO: later refactor this to be cleaner

        // MELEE Weapon
        if (weaponData.isMelee)
        {
            // Center of the weapon
            float radius = weaponData.attackRange * 0.5f;

            Vector3 attackCenterPoint = transform.position + direction * radius;

            Collider[] hits = Physics.OverlapSphere(attackCenterPoint, radius);

            // Avoid collider hit multiple time on the same enemy
            HashSet<HealthComponent> damaged = new();

            foreach (Collider hit in hits)
            {
                HealthComponent enemyHealth = hit.GetComponent<HealthComponent>();

                if (enemyHealth != null && enemyHealth != currentHealthComponent && !damaged.Contains(enemyHealth))
                {
                    damaged.Add(enemyHealth);
                    enemyHealth.TakeDamage(weaponData.attackDamage);
                }
            }
        }
    }

    private void OnDestroy()
    {
        if (isPlayer && InputManager.Instance != null)
        {
            InputManager.Instance.OnAttackPressed -= InputManager_OnAttackPressed;
        }
    }
}
