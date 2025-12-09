using System;
using UnityEngine;

public abstract class SkillExecutorView : EntityView
{
    public abstract SkillCategory Category { get; }

    protected virtual void Start()
    {
        WorldInstance.Events.Subscribe<SkillConfirmExecutionEvent>(OnSkillConfirmExecutionEvent);

        WorldInstance.Events.Subscribe<SkillEffectTriggerEvent>(OnSkillEffectTriggerEvent);
    }

    // CREATE SKILL EFFECT/VFX/PREFAB
    private void OnSkillEffectTriggerEvent(SkillEffectTriggerEvent @event)
    {
        if (@event.Skill == null || @event.Skill.category != Category)
        {
            return;
        }

        if (@event.Caster != EntityInstance)
        {
            return;
        }

        // Play skill activation sound
        if (@event.Skill.activateSound != null)
        {
            var registry = WorldInstance.Services.Resolve<EntityViewRegistry>();
            if (registry.TryGet(EntityInstance, out EntityView view))
            {
                AudioHelper.PlaySound3D(
                    WorldInstance,
                    @event.Skill.activateSound,
                    AudioCategory.Skill,
                    view.transform.position
                );
            }
        }

        // Animation
        WorldInstance.Events.Publish(
            new AnimationParameterEvent(
                EntityInstance,
                @event.Skill.activationAnimationTrigger,
                AnimationParameterType.Trigger,
                null
            )
        );

        // Store the skill to buffer to trigger it later
        var skillBuffer = WorldInstance.Components.Get<SkillCastBufferComponent>(EntityInstance);

        skillBuffer.Skill = @event.Skill;
        skillBuffer.Direction = @event.Direction;
        skillBuffer.TargetPoint = @event.TargetPoint;
    }

    protected virtual void OnSkillConfirmExecutionEvent(SkillConfirmExecutionEvent @event)
    {
        ExecuteSkill(@event);
    }

    protected virtual void ExecuteSkill(SkillConfirmExecutionEvent @event)
    {
        WorldInstance.Events.Publish(
            new ExitCombatStateEvent { Entity = @event.Caster, TargetState = CombatState.CastingSkill }
        );
    }

    protected void FinishSkill(SkillDefinitionSO skill)
    {
        WorldInstance.Events.Publish(new SkillExecutionFinishedEvent { Caster = EntityInstance, Skill = skill });
    }

    protected virtual void OnDestroy()
    {
        WorldInstance.Events.Unsubscribe<SkillConfirmExecutionEvent>(OnSkillConfirmExecutionEvent);
        WorldInstance.Events.Unsubscribe<SkillEffectTriggerEvent>(OnSkillEffectTriggerEvent);
    }
}
