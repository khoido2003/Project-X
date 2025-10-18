using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DashStrikeExecutorView : SkillExecutorView
{
    public override SkillCategory Category => SkillCategory.DashStrike;

    protected override void ExecuteSkill(SkillConfirmExecutionEvent @event)
    {
        base.ExecuteSkill(@event);

        EntityViewRegistry registry = WorldInstance.Services.Resolve<EntityViewRegistry>();
        if (!registry.TryGet(@event.Caster, out EntityView view))
        {
            return;
        }

        GameObject owner = view.gameObject;
        DashStrikeSkillSO dashSkill = @event.Skill as DashStrikeSkillSO;
        if (dashSkill == null)
        {
            return;
        }

        StartCoroutine(DashRoutine(owner, dashSkill, @event.TargetPoint));
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
                Instantiate(skill.hitVfxPrefab, hit.ClosestPoint(hitPoint), Quaternion.identity);
            }
        }
    }
}
