using Unity.Netcode;
using UnityEngine;

public class ExplosiveShotExecutorView : SkillExecutorView
{
    public override SkillCategory Category => SkillCategory.ExplosiveShot;

    protected override void ExecuteSkill(SkillConfirmExecutionEvent @event)
    {
        if (!NetworkManager.Singleton.IsServer)
        {
            return;
        }

        if (@event.Skill is not ExplosiveShotSkillSO skill)
        {
            return;
        }

        if (!WorldInstance.Services.Resolve<EntityViewRegistry>().TryGet(@event.Caster, out EntityView casterView))
        {
            return;
        }

        // Trigger aiming rig if enabled
        TriggerAimingRig(casterView, @event.TargetPoint, @event.Direction);

        FireExplosiveProjectile(casterView, skill, @event.Direction, @event.TargetPoint);

        base.ExecuteSkill(@event);
    }

    private void FireExplosiveProjectile(
        EntityView casterView,
        ExplosiveShotSkillSO skill,
        Vector3 direction,
        Vector3 targetPoint
    )
    {
        Transform casterTransform = casterView.transform;

        // Calculate direction
        Vector3 shotDirection = direction;
        if (shotDirection.sqrMagnitude < 0.001f)
        {
            shotDirection = casterTransform.forward;
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

        if (skill.projectilePrefab != null)
        {
            if (skill.projectilePrefab.GetComponent<NetworkObject>() == null)
            {
                Debug.LogError(
                    $"[ExplosiveShotExecutorView] Projectile prefab {skill.projectilePrefab.name} must have NetworkObject component!"
                );
                return;
            }

            // Spawn as NetworkObject for proper network synchronization
            GameObject projectileGO = NetworkObjectSpawner.SpawnNewNetworkObject(
                skill.projectilePrefab,
                spawnPos,
                spawnRot,
                true
            );

            // Remove ProjectileView if it exists (use ExplosiveProjectileView instead)
            var existingProjectile = projectileGO.GetComponent<ProjectileView>();
            if (existingProjectile != null)
            {
                Destroy(existingProjectile);
            }

            // Add explosive projectile component
            ExplosiveProjectileView explosiveComp =
                projectileGO.GetComponent<ExplosiveProjectileView>()
                ?? projectileGO.AddComponent<ExplosiveProjectileView>();

            // Initialize explosive component
            explosiveComp.Initialize(
                WorldInstance,
                EntityInstance,
                skill.explosionRadius,
                skill.explosionDamage,
                skill.explosionVfxPrefab,
                skill.projectileSpeed,
                skill.projectileLifetime,
                shotDirection
            );
        }

        FinishSkill(skill);
    }

    private void TriggerAimingRig(EntityView casterView, Vector3 targetPoint, Vector3 direction)
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

                // Start aiming for the duration of the skill cast
                aimingRig.StartAiming(aimTarget, 1.5f); // Aim for 1.5 seconds
            }
        }
    }

    /// <summary>
    /// For ExplosiveShot, we DON'T spawn a client-side visual projectile.
    /// The server spawns a NetworkObject that automatically syncs to clients.
    /// When it explodes, ExplodeClientRpc() handles the explosion VFX on clients.
    /// Spawning a separate visual here would cause duplicate projectiles.
    /// </summary>
    protected override void SpawnClientVisualEffect(SkillEffectTriggerEvent @event)
    {
        // Intentionally empty - ExplosiveProjectileView handles client sync via NetworkObject
        // and explosion VFX via ExplodeClientRpc()
    }
}
