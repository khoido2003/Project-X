using System;
using System.Collections.Generic;
using UnityEngine;

public abstract class SkillExecutorView : EntityView
{
    public abstract SkillCategory Category { get; }

    protected virtual void Start()
    {
        WorldInstance.Events.Subscribe<SkillExecutionRequestEvent>(OnSkillExecution);
    }

    protected virtual void OnSkillExecution(SkillExecutionRequestEvent @event)
    {
        if (@event.Skill == null || @event.Skill.category != Category)
        {
            return;
        }
        Debug.Log($"SkillExecutionEvent received for category {@event.Skill?.category}, executor expects {Category}");

        ExecuteSkill(@event);
    }

    protected virtual void ExecuteSkill(SkillExecutionRequestEvent @event)
    {
        if (WorldInstance.Components.TryGet(@event.Caster, out CombatStateComponent state))
        {
            state.CurrentState = CombatState.Idle;
        }
    }

    protected virtual void OnDestroy()
    {
        WorldInstance.Events.Unsubscribe<SkillExecutionRequestEvent>(OnSkillExecution);
    }
}
