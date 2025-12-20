using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class HomerunSwingExecutorView : SkillExecutorView
{
    public override SkillCategory Category => SkillCategory.HomerunSwing;

    protected override void ExecuteSkill(SkillConfirmExecutionEvent @event)
    {
        if (!NetworkManager.Singleton.IsServer)
        {
            return;
        }

        if (@event.Skill is not HomerunSwingSkillSO skill)
        {
            return;
        }

        if (!WorldInstance.Services.Resolve<EntityViewRegistry>().TryGet(@event.Caster, out EntityView casterView))
        {
            return;
        }

        // Find skill index for broadcasting hit VFX
        int skillIndex = GetSkillIndex(skill);
        StartCoroutine(ChargeAndSwing(casterView, skill, @event.Direction, skillIndex));

        base.ExecuteSkill(@event);
    }

    /// <summary>
    /// Called on CLIENT to spawn visual effects for the swing
    /// </summary>
    protected override void SpawnClientVisualEffect(SkillEffectTriggerEvent @event)
    {
        if (@event.Skill is not HomerunSwingSkillSO skill)
        {
            return;
        }

        if (!WorldInstance.Services.Resolve<EntityViewRegistry>().TryGet(@event.Caster, out EntityView casterView))
        {
            return;
        }

        // Spawn swing VFX on client
        StartCoroutine(ClientSwingVisualRoutine(casterView, skill, @event.Direction));
    }

    private int GetSkillIndex(SkillDefinitionSO skill)
    {
        if (!WorldInstance.Components.TryGet(EntityInstance, out SkillSetComponent skillSet))
        {
            return -1;
        }

        for (int i = 0; i < skillSet.Skills.Count; i++)
        {
            if (skillSet.Skills[i] == skill)
            {
                return i;
            }
        }
        return -1;
    }

    private IEnumerator ClientSwingVisualRoutine(EntityView casterView, HomerunSwingSkillSO skill, Vector3 direction)
    {
        // Wait for charge duration before showing swing VFX
        yield return new WaitForSeconds(skill.chargeDuration);

        Transform ownerTransform = casterView.transform;
        Vector3 origin = ownerTransform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f)
        {
            direction = ownerTransform.forward;
        }

        // Spawn swing VFX
        if (skill.swingVfxPrefab != null)
        {
            var swingFx = Instantiate(skill.swingVfxPrefab, origin, Quaternion.LookRotation(direction));
            Destroy(swingFx.gameObject, 2f);
        }
    }

    private IEnumerator ChargeAndSwing(
        EntityView casterView,
        HomerunSwingSkillSO skill,
        Vector3 direction,
        int skillIndex
    )
    {
        yield return new WaitForSeconds(skill.chargeDuration);

        Transform ownerTransform = casterView.transform;

        Vector3 origin = ownerTransform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f)
        {
            direction = ownerTransform.forward;
        }

        Collider[] hits = Physics.OverlapSphere(origin, skill.attackRadius);
        foreach (Collider hit in hits)
        {
            if (!hit.TryGetComponent(out EntityView targetView))
            {
                continue;
            }

            if (targetView.EntityInstance.Equals(EntityInstance))
            {
                continue;
            }

            Vector3 toEnemy = hit.transform.position - origin;

            toEnemy.y = 0f;

            if (toEnemy.sqrMagnitude < 0.001f)
            {
                continue;
            }

            float angle = Vector3.Angle(direction.normalized, toEnemy.normalized);

            if (angle > skill.coneAngle * 0.5f)
            {
                continue;
            }

            WorldInstance.Events.Publish(
                new DamageEvent
                {
                    Amount = skill.damage,
                    Attacker = EntityInstance,
                    Target = targetView.EntityInstance,
                }
            );

            WorldInstance.Events.Publish(
                new KnockbackEvent
                {
                    Target = targetView.EntityInstance,
                    Direction = direction.normalized,
                    Force = skill.knockbackForce,
                }
            );

            WorldInstance.Events.Publish(
                new StunEvent { Target = targetView.EntityInstance, Duration = skill.stunDuration }
            );

            Vector3 hitPoint = hit.ClosestPoint(origin);

            // Spawn hit VFX on server
            if (skill.hitVfxPrefab)
            {
                var hitVfx = Instantiate(skill.hitVfxPrefab, hitPoint, Quaternion.identity);
                Destroy(hitVfx.gameObject, 2f);
            }

            // Broadcast hit VFX to clients
            if (
                skillIndex >= 0
                && WorldInstance.Components.TryGet(EntityInstance, out NetworkSyncComponent sync)
                && sync.SyncView != null
            )
            {
                sync.SyncView.BroadcastSkillHitVfxClientRpc(hitPoint, skillIndex);
            }

            break;
        }

        if (skill.swingVfxPrefab)
        {
            var swingFx = Instantiate(skill.swingVfxPrefab, origin, Quaternion.LookRotation(direction));
            Destroy(swingFx.gameObject, 2f);
        }

        FinishSkill(skill);
    }
}
