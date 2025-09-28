using System;
using System.Collections.Generic;
using UnityEngine;

public class AttackComponent : MonoBehaviour, IAnimationTrigger, IAnimationRelayReceiver
{
    protected WeaponData weaponData;
    private HealthComponent currentHealthComponent;

    private float lastAttackTime = -Mathf.Infinity;
    private bool isPlayer;

    private Vector3 hitDirection;

    public event Action<string> OnTriggerAnimation;
    public event Action<string, float> OnSetFloatParameter;
    public event Action<string, bool> OnSetBoolParameter;

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

        // TRIGGER ANIMATION HERE
        hitDirection = transform.forward * (GetComponent<Character>()?.Data?.forwardDirectionMultiplier ?? 1f);

        // Choose random attack
        int randomIndex = UnityEngine.Random.Range(0, weaponData.totalAttackAnimations);

        OnSetFloatParameter?.Invoke("attackIndex", randomIndex);

        OnTriggerAnimation?.Invoke(weaponData.attackAnimationTrigger);

        // TODO: Trigger sound here

        lastAttackTime = Time.time;
    }

    public void OnAnimationEvent(AnimationEventRelayName eventName)
    {
        if (eventName == AnimationEventRelayName.ATTACK_HIT)
        {
            PerformHit();
        }
    }

    public void PerformHit()
    {
        ExcuteAttack(hitDirection);
    }

    protected virtual void ExcuteAttack(Vector3 direction)
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

                    // Spawn Hit Effect
                    if (weaponData.hitImpactParticlePrefab != null)
                    {
                        ParticleSystem impactInstance = Instantiate(
                            weaponData.hitImpactParticlePrefab,
                            hit.ClosestPoint(transform.position),
                            Quaternion.identity
                        );
                        Destroy(impactInstance.gameObject, 2f);
                    }
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
