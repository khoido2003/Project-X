using System.Collections;
using UnityEngine;

public class PlasmaShieldExecutorView : SkillExecutorView
{
    public override SkillCategory Category => SkillCategory.PlasmaShield;

    protected override void ExecuteSkill(SkillConfirmExecutionEvent @event)
    {
        base.ExecuteSkill(@event);

        if (!(@event.Skill is PlasmaShieldSkillSO skill))
        {
            return;
        }

        WorldInstance.Events.Publish(
            new ApplyBuffEvent
            {
                Target = @event.Caster,
                BuffType = BuffType.DefenseBoost,
                Value = skill.defenseBoost,
                Duration = skill.boostDuration,
            }
        );

        if (skill.shieldPrefab != null)
        {
            var shield = Instantiate(skill.shieldPrefab, transform);

            Destroy(shield.gameObject, skill.boostDuration);
        }
    }
}
