using System.Collections;
using Unity.VisualScripting;
using UnityEngine;

public class DashStrikeExecutorView : SkillExecutorView
{
    public override SkillCategory Category => SkillCategory.DashStrike;

    protected override void ExecuteSkill(SkillExecutionRequestEvent @event)
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

        StartCoroutine(DashRoutine(owner, dashSkill, @event.Direction));
    }

    private IEnumerator DashRoutine(GameObject owner, DashStrikeSkillSO skill, Vector3 direction)
    {
        CharacterController controller = owner.GetComponent<CharacterController>();

        if (controller == null)
        {
            yield break;
        }

        Vector3 start = owner.transform.position;
        Vector3 end = start + direction.normalized * skill.dashDistance;
        float elapsed = 0f;

        TrailRenderer trail = null;
        if (skill.dashTrailPrefab)
        {
            trail = Object.Instantiate(skill.dashTrailPrefab, owner.transform);
        }

        while (elapsed < skill.dashDuration)
        {
            elapsed += Time.deltaTime;
            Vector3 newPos = Vector3.Lerp(start, end, elapsed / skill.dashDuration);
            controller.Move(newPos - owner.transform.position);

            yield return null;
        }

        if (trail)
        {
            trail.transform.SetParent(null);
            Object.Destroy(trail.gameObject, 1f);
        }

        Collider[] hits = Physics.OverlapSphere(end, skill.attackRadius);
        foreach (var hit in hits)
        {
            if (hit.TryGetComponent(out HealthComponent health))
            {
                health.TakeDamage(skill.damage);

                if (skill.hitVfxPrefab)
                {
                    Object.Instantiate(skill.hitVfxPrefab, hit.ClosestPoint(end), Quaternion.identity);
                }
            }
        }
    }
}
