using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class PlasmaShieldExecutorView : SkillExecutorView
{
    public override SkillCategory Category => SkillCategory.PlasmaShield;

    protected override void ExecuteSkill(SkillConfirmExecutionEvent @event)
    {
        if (!NetworkManager.Singleton.IsServer)
        {
            return;
        }

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

        // Spawn shield VFX on server
        if (skill.shieldPrefab != null)
        {
            var shield = Instantiate(skill.shieldPrefab, transform);
            Destroy(shield.gameObject, skill.boostDuration);
        }

        base.ExecuteSkill(@event);

        FinishSkill(skill);
    }

    /// <summary>
    /// Called on CLIENT to spawn visual effects for the shield
    /// </summary>
    protected override void SpawnClientVisualEffect(SkillEffectTriggerEvent @event)
    {
        if (!(@event.Skill is PlasmaShieldSkillSO skill))
        {
            return;
        }

        if (!WorldInstance.Services.Resolve<EntityViewRegistry>().TryGet(@event.Caster, out EntityView casterView))
        {
            return;
        }

        // Spawn shield VFX on client
        if (skill.shieldPrefab != null)
        {
            var shield = Instantiate(skill.shieldPrefab, casterView.transform);
            Destroy(shield.gameObject, skill.boostDuration);
        }
    }
}

