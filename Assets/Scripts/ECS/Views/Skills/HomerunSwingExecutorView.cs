using System.Collections;
using UnityEngine;

public class HomerunSwingExecutorView : SkillExecutorView
{
    public override SkillCategory Category => SkillCategory.HomerunSwing;

    protected override void ExecuteSkill(SkillConfirmExecutionEvent @event)
    {
        base.ExecuteSkill(@event);

        if (@event.Skill is not HomerunSwingSkillSO skill)
        {
            return;
        }

        if (!WorldInstance.Services.Resolve<EntityViewRegistry>().TryGet(@event.Caster, out EntityView casterView))
        {
            return;
        }
        StartCoroutine(ChargeAndSwing(casterView, skill, @event.Direction));
    }

    private IEnumerator ChargeAndSwing(EntityView casterView, HomerunSwingSkillSO skill, Vector3 direction)
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

            if (skill.hitVfxPrefab)
            {
                Vector3 hitPoint = hit.ClosestPoint(origin);
                Instantiate(skill.hitVfxPrefab, hitPoint, Quaternion.identity);
            }

            break;
        }

        if (skill.swingVfxPrefab)
        {
            var swingFx = Instantiate(skill.swingVfxPrefab, origin, Quaternion.LookRotation(direction));
            Destroy(swingFx.gameObject, 2f);
        }
    }
}
