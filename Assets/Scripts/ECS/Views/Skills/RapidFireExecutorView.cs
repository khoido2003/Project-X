using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class RapidFireExecutorView : SkillExecutorView
{
    public override SkillCategory Category => SkillCategory.RapidFire;

    protected override void ExecuteSkill(SkillConfirmExecutionEvent @event)
    {
        if (!NetworkManager.Singleton.IsServer)
        {
            return;
        }

        if (@event.Skill is not RapidFireSkillSO skill)
        {
            return;
        }

        if (!WorldInstance.Services.Resolve<EntityViewRegistry>().TryGet(@event.Caster, out EntityView casterView))
        {
            return;
        }

        // Trigger aiming rig if enabled
        TriggerAimingRig(casterView, @event.TargetPoint, @event.Direction, skill);

        StartCoroutine(RapidFireRoutine(casterView, skill, @event.Direction, @event.TargetPoint));

        base.ExecuteSkill(@event);
    }

    private IEnumerator RapidFireRoutine(
        EntityView casterView,
        RapidFireSkillSO skill,
        Vector3 direction,
        Vector3 targetPoint
    )
    {
        Transform casterTransform = casterView.transform;
        Vector3 baseDirection = direction;

        // If direction is invalid, use transform forward
        if (baseDirection.sqrMagnitude < 0.001f)
        {
            baseDirection = casterTransform.forward;
        }
        baseDirection.y = 0f;
        baseDirection = baseDirection.normalized;

        // Find projectile spawn position
        Transform spawnTransform = casterTransform;
        ProjectileSpawnPos spawnPosComponent = casterTransform.GetComponentInChildren<ProjectileSpawnPos>();
        if (spawnPosComponent != null)
        {
            spawnTransform = spawnPosComponent.transform;
        }

        ObjectPoolService pool = WorldInstance.Services.Resolve<ObjectPoolService>();

        for (int i = 0; i < skill.projectileCount; i++)
        {
            // Calculate spread angle for this shot
            float spread = Random.Range(-skill.spreadAngle, skill.spreadAngle);
            Quaternion spreadRotation = Quaternion.Euler(0, spread, 0);
            Vector3 shotDirection = spreadRotation * baseDirection;

            // Spawn position
            Vector3 spawnPos = spawnTransform.position;
            if (spawnPosComponent == null)
            {
                spawnPos = casterTransform.position + new Vector3(0f, 1.3f, 0f);
            }

            Quaternion spawnRot = Quaternion.LookRotation(shotDirection, Vector3.up);

            // Spawn projectile
            if (skill.projectilePrefab != null)
            {
                GameObject projectileGO = pool.Get(skill.projectilePrefab, spawnPos, spawnRot);

                if (!projectileGO.TryGetComponent(out ProjectileView projectile))
                {
                    projectile = projectileGO.AddComponent<ProjectileView>();
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

            // Wait before next shot
            if (i < skill.projectileCount - 1)
            {
                yield return new WaitForSeconds(skill.timeBetweenShots);
            }
        }

        FinishSkill(skill);
    }

    private void TriggerAimingRig(EntityView casterView, Vector3 targetPoint, Vector3 direction, RapidFireSkillSO skill)
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

                // Calculate aiming duration based on rapid fire duration
                float aimDuration = skill.projectileCount * skill.timeBetweenShots + 0.5f;
                aimingRig.StartAiming(aimTarget, aimDuration);
            }
        }
    }
}
