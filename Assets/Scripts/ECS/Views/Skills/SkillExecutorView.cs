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
        if (WorldInstance.Components.TryGet(@event.Caster, out CombatStateComponent state))
        {
            state.CurrentState = CombatState.Idle;
        }
    }

    protected virtual void OnDestroy()
    {
        WorldInstance.Events.Unsubscribe<SkillConfirmExecutionEvent>(OnSkillConfirmExecutionEvent);
        WorldInstance.Events.Unsubscribe<SkillEffectTriggerEvent>(OnSkillEffectTriggerEvent);
    }
}
