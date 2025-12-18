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

        // Play skill sound - simplified!
        if (@event.Skill.activateSound != null)
        {
            var registry = WorldInstance.Services.Resolve<EntityViewRegistry>();
            if (registry.TryGet(EntityInstance, out EntityView view))
            {
                // Category automatically determined by system
                var category = WorldInstance.Components.Has<PlayerTagComponent>(EntityInstance)
                    ? AudioCategory.Player
                    : AudioCategory.Enemy;

                AudioHelper.PlaySound3D(WorldInstance, @event.Skill.activateSound, category, view.transform.position);
            }
        }
        else
        {
            // Profile-driven fallback
            WorldInstance.Events.Publish(new AudioCueEvent(EntityInstance, SoundType.Skill, @event.TargetPoint));
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

        // Buffer skill for execution
        var skillBuffer = WorldInstance.Components.Get<SkillCastBufferComponent>(EntityInstance);
        skillBuffer.Skill = @event.Skill;
        skillBuffer.Direction = @event.Direction;
        skillBuffer.TargetPoint = @event.TargetPoint;
    }

    protected virtual void OnSkillConfirmExecutionEvent(SkillConfirmExecutionEvent @event)
    {
        // Only execute if this event is for this entity
        if (@event.Caster != EntityInstance)
        {
            return;
        }

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
