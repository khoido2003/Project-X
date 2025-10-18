using System;
using UnityEngine;

public abstract class SkillExecutorView : EntityView
{
    public abstract SkillCategory Category { get; }

    protected virtual void Start()
    {
        WorldInstance.Events.Subscribe<SkillConfirmExecutionEvent>(OnSkillConfirmExecutionEvent);
    }

    protected virtual void OnSkillConfirmExecutionEvent(SkillConfirmExecutionEvent @event)
    {
        if (@event.Skill == null || @event.Skill.category != Category)
        {
            return;
        }

        if (@event.Caster != EntityInstance)
        {
            return;
        }

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
    }
}
