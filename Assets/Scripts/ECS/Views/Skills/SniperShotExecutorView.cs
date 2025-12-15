using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class SniperShotExecutorView : SkillExecutorView
{
    public override SkillCategory Category => SkillCategory.SniperShot;

    protected override void ExecuteSkill(SkillConfirmExecutionEvent @event)
    {
        if (!NetworkManager.Singleton.IsServer)
        {
            return;
        }

        if (@event.Skill is not SniperShotSkillSO skill)
        {
            return;
        }

        if (!WorldInstance.Services.Resolve<EntityViewRegistry>().TryGet(@event.Caster, out EntityView casterView))
        {
            return;
        }

        // Trigger aiming rig if enabled
        TriggerAimingRig(casterView, @event.TargetPoint, @event.Direction, skill);

        StartCoroutine(ChargeAndFire(casterView, skill, @event.Direction, @event.TargetPoint));

        base.ExecuteSkill(@event);
    }

    private IEnumerator ChargeAndFire(
        EntityView casterView,
        SniperShotSkillSO skill,
        Vector3 direction,
        Vector3 targetPoint
    )
    {
        Transform casterTransform = casterView.transform;

        // Spawn charge VFX
        ParticleSystem chargeVfx = null;
        if (skill.chargeVfxPrefab != null)
        {
            chargeVfx = Instantiate(
                skill.chargeVfxPrefab,
                casterTransform.position + Vector3.up * 1.5f,
                Quaternion.identity,
                casterTransform
            );
        }

        // Charge duration
        yield return new WaitForSeconds(skill.chargeDuration);

        // Clean up charge VFX
        if (chargeVfx != null)
        {
            Destroy(chargeVfx.gameObject);
        }

        // Calculate direction
        Vector3 shotDirection = direction;
        if (shotDirection.sqrMagnitude < 0.001f)
        {
            // If no direction, try to aim at target point
            if (targetPoint.sqrMagnitude > 0.001f)
            {
                shotDirection = (targetPoint - casterTransform.position).normalized;
            }
            else
            {
                shotDirection = casterTransform.forward;
            }
        }
        shotDirection.y = 0f;
        shotDirection = shotDirection.normalized;

        // Find projectile spawn position
        Transform spawnTransform = casterTransform;
        ProjectileSpawnPos spawnPosComponent = casterTransform.GetComponentInChildren<ProjectileSpawnPos>();

        if (spawnPosComponent != null)
        {
            spawnTransform = spawnPosComponent.transform;
        }

        Vector3 spawnPos = spawnTransform.position;
        if (spawnPosComponent == null)
        {
            spawnPos = casterTransform.position + new Vector3(0f, 1.3f, 0f);
        }

        Quaternion spawnRot = Quaternion.LookRotation(shotDirection, Vector3.up);

        ObjectPoolService pool = WorldInstance.Services.Resolve<ObjectPoolService>();

        if (skill.projectilePrefab != null)
        {
            GameObject projectileGO = pool.Get(skill.projectilePrefab, spawnPos, spawnRot);

            if (!projectileGO.TryGetComponent(out ProjectileView projectile))
            {
                projectile = projectileGO.AddComponent<ProjectileView>();
            }

            // Ensure the projectile has NetworkObject for networking
            if (!projectileGO.TryGetComponent(out NetworkObject netObj))
            {
                netObj = projectileGO.AddComponent<NetworkObject>();
            }

            // Spawn on network if not already spawned
            if (!netObj.IsSpawned)
            {
                netObj.Spawn();
            }

            // Add piercing component if enabled
            if (skill.canPierce)
            {
                PiercingProjectileView piercingComp = projectileGO.GetComponent<PiercingProjectileView>();
                if (piercingComp == null)
                {
                    piercingComp = projectileGO.AddComponent<PiercingProjectileView>();
                }

                piercingComp.Initialize(WorldInstance, EntityInstance, skill.maxPierceCount);
            }

            projectile.Initialize(
                WorldInstance,
                EntityInstance,
                skill.damage,
                skill.projectileSpeed,
                skill.projectileLifetime,
                shotDirection,
                skill.hitVfxPrefab,
                skill.projectilePrefab,
                spawnPos,
                spawnRot
            );
        }

        FinishSkill(skill);
    }

    private void TriggerAimingRig(EntityView casterView, Vector3 targetPoint, Vector3 direction, SniperShotSkillSO skill)
    {
        AimingRigView aimingRig = casterView.GetComponent<AimingRigView>();
        if (aimingRig == null)
        {
            return;
        }

        // Check if character has aiming rig enabled
        if (WorldInstance.Components.TryGet(casterView.EntityInstance, out CharacterSelectionComponent characterSelection))
        {
            if (characterSelection.CharacterData != null && characterSelection.CharacterData.useAimingRig)
            {
                Vector3 aimTarget = targetPoint;
                if (aimTarget.sqrMagnitude < 0.001f && direction.sqrMagnitude > 0.001f)
                {
                    // Use direction to calculate target point
                    aimTarget = casterView.transform.position + direction.normalized * 10f;
                }

                // Aim for the charge duration plus a bit extra
                float aimDuration = skill.chargeDuration + 1f;
                aimingRig.StartAiming(aimTarget, aimDuration);
            }
        }
    }
}
