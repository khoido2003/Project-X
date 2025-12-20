using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class DashStrikeExecutorView : SkillExecutorView
{
    public override SkillCategory Category => SkillCategory.DashStrike;

    protected override void ExecuteSkill(SkillConfirmExecutionEvent @event)
    {
        if (!NetworkManager.Singleton.IsServer)
        {
            return;
        }

        if (@event.Skill is not DashStrikeSkillSO skill)
        {
            return;
        }

        EntityViewRegistry registry = WorldInstance.Services.Resolve<EntityViewRegistry>();
        if (!registry.TryGet(@event.Caster, out EntityView view))
        {
            return;
        }

        GameObject owner = view.gameObject;

        StartCoroutine(DashRoutine(owner, skill, @event.TargetPoint));

        base.ExecuteSkill(@event);
    }

    /// <summary>
    /// Called on CLIENT to spawn visual effects for the dash
    /// </summary>
    protected override void SpawnClientVisualEffect(SkillEffectTriggerEvent @event)
    {
        if (@event.Skill is not DashStrikeSkillSO skill)
        {
            return;
        }

        EntityViewRegistry registry = WorldInstance.Services.Resolve<EntityViewRegistry>();
        if (!registry.TryGet(@event.Caster, out EntityView view))
        {
            return;
        }

        // Spawn dash trail effect on client
        StartCoroutine(ClientDashVisualRoutine(view.gameObject, skill, @event.TargetPoint));
    }

    private IEnumerator ClientDashVisualRoutine(GameObject owner, DashStrikeSkillSO skill, Vector3 targetPoint)
    {
        TrailRenderer trail = null;
        if (skill.dashTrailPrefab != null)
        {
            trail = Instantiate(skill.dashTrailPrefab, owner.transform);
        }

        // Wait for dash duration
        yield return new WaitForSeconds(skill.dashDuration);

        // Clean up trail
        if (trail != null)
        {
            trail.transform.SetParent(null);
            Destroy(trail.gameObject, 1f);
        }
    }

    private IEnumerator DashRoutine(GameObject owner, DashStrikeSkillSO skill, Vector3 targetPoint)
    {
        CharacterController controller = owner.GetComponent<CharacterController>();

        if (controller == null)
        {
            yield break;
        }

        TrailRenderer trail = null;
        if (skill.dashTrailPrefab)
        {
            trail = Instantiate(skill.dashTrailPrefab, owner.transform);
        }

        Vector3 start = owner.transform.position;
        float elapsed = 0f;
        targetPoint.y = start.y;

        while (elapsed < skill.dashDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / skill.dashDuration);

            Vector3 newPos = Vector3.Lerp(start, targetPoint, t);
            controller.Move(newPos - owner.transform.position);

            yield return null;
        }

        if (trail)
        {
            trail.transform.SetParent(null);
            Destroy(trail.gameObject, 1f);
        }

        ApplyDashDamage(skill, targetPoint);

        FinishSkill(skill);
    }

    private void ApplyDashDamage(DashStrikeSkillSO skill, Vector3 hitPoint)
    {
        HashSet<EntityId> damageCache = new();

        Collider[] hits = Physics.OverlapSphere(hitPoint, skill.attackRadius);
        foreach (var hit in hits)
        {
            if (!hit.TryGetComponent(out EntityView targetView))
            {
                continue;
            }

            EntityId targetEntity = targetView.EntityInstance;
            if (targetEntity.Equals(EntityInstance))
            {
                continue;
            }

            if (damageCache.Contains(targetEntity))
            {
                continue;
            }

            damageCache.Add(targetEntity);

            WorldInstance.Events.Publish(
                new DamageEvent
                {
                    Target = targetEntity,
                    Attacker = EntityInstance,
                    Amount = skill.damage,
                }
            );

            if (skill.hitVfxPrefab)
            {
                var hitVfx = Instantiate(skill.hitVfxPrefab, hit.ClosestPoint(hitPoint), Quaternion.identity);
                Destroy(hitVfx.gameObject, 2f); // Ensure VFX is destroyed
            }
        }
    }
}

