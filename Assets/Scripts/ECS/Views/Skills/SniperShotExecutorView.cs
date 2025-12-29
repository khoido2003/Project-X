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

        // Find projectile spawn position FIRST (gun muzzle)
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

        // Calculate direction FROM gun muzzle position TO target
        // This fixes the parallax offset issue where projectile didn't appear to come from the gun
        Vector3 shotDirection;
        if (targetPoint.sqrMagnitude > 0.001f)
        {
            // Calculate direction from spawn position to target point
            // Project to same height to get horizontal direction
            Vector3 targetHorizontal = new Vector3(targetPoint.x, spawnPos.y, targetPoint.z);
            shotDirection = (targetHorizontal - spawnPos).normalized;
        }
        else if (direction.sqrMagnitude > 0.001f)
        {
            // If we have a direction, calculate a far target point and aim from spawn pos
            Vector3 farTarget = casterTransform.position + direction.normalized * 100f;
            farTarget.y = spawnPos.y;
            shotDirection = (farTarget - spawnPos).normalized;
        }
        else
        {
            shotDirection = casterTransform.forward;
        }
        
        shotDirection.y = 0f;
        shotDirection = shotDirection.normalized;

        Quaternion spawnRot = Quaternion.LookRotation(shotDirection, Vector3.up);



        if (skill.projectilePrefab != null)
        {
            if (skill.projectilePrefab.GetComponent<NetworkObject>() == null)
            {
                Debug.LogError(
                    $"[SniperShotExecutorView] Projectile prefab {skill.projectilePrefab.name} must have NetworkObject component!"
                );
                yield break;
            }

            GameObject projectileGO = NetworkObjectSpawner.SpawnNewNetworkObject(
                skill.projectilePrefab,
                spawnPos,
                spawnRot,
                true
            );

            if (!projectileGO.TryGetComponent(out ProjectileView projectile))
            {
                projectile = projectileGO.AddComponent<ProjectileView>();
            }

            // Initialize projectile with high damage from skill (no piercing, simple single-target hit)
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

    private void TriggerAimingRig(
        EntityView casterView,
        Vector3 targetPoint,
        Vector3 direction,
        SniperShotSkillSO skill
    )
    {
        AimingRigView aimingRig = casterView.GetComponent<AimingRigView>();
        if (aimingRig == null)
        {
            return;
        }

        // Check if character has aiming rig enabled
        if (
            WorldInstance.Components.TryGet(
                casterView.EntityInstance,
                out CharacterSelectionComponent characterSelection
            )
        )
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

    /// <summary>
    /// For SniperShot, we DON'T spawn a client-side visual projectile.
    /// The server spawns a NetworkObject that automatically syncs to clients.
    /// Spawning a separate visual here would cause duplicate projectiles.
    /// </summary>
    protected override void SpawnClientVisualEffect(SkillEffectTriggerEvent @event)
    {
        // Intentionally empty - NetworkObject projectile syncs to clients automatically
        // Impact VFX is handled by ProjectileView.HitAndReturn on the networked object
    }
}
