using System;
using UnityEngine;

public class SkillSystem : ISystem
{
    public World _world;
    private EntityViewRegistry _registry;

    private SkillDefinitionSO currentChosenSkill;

    public void Initialize(World world)
    {
        _world = world;
        _registry = world.Services.Resolve<EntityViewRegistry>();

        _world.Events.Subscribe<SkillPressedInputEvent>(OnSkillPressedInput);
    }

    public void Update(float dt) { }

    public void FixedUpdate(float dt) { }

    private void OnSkillPressedInput(SkillPressedInputEvent @event)
    {
        if (!_world.Components.TryGet(@event.Entity, out SkillSetComponent skillSet))
        {
            return;
        }

        int index = @event.SkillIndex - 1;
        if (index < 0 || index >= skillSet.Skills.Count)
        {
            return;
        }

        if (Time.time < skillSet.CooldownUntil[index])
        {
            return;
        }

        SkillDefinitionSO skill = skillSet.Skills[index];
        if (skill == null)
        {
            return;
        }

        currentChosenSkill = skill;

        if (!_world.Components.TryGet(@event.Entity, out CombatStateComponent state))
        {
            state = new CombatStateComponent { CurrentState = CombatState.Idle };
            _world.Components.Add(@event.Entity, state);
        }

        if (state.CurrentState == CombatState.Attacking)
        {
            return;
        }

        if (!_world.Components.TryGet(@event.Entity, out ActionFlagComponent flags))
        {
            return;
        }

        if (@event.IsPressed)
        {
            if (skill.isInstant)
            {
                ExecuteSkill(@event.Entity, currentChosenSkill);
            }
            else
            {
                flags.Set(ActionFlag.SkillPreview, true);
                _world.Events.Publish(new SkillPreviewRequestEvent(@event.Entity, skill, true));
            }
        }
        else
        {
            flags.Set(ActionFlag.SkillPreview, false);
            _world.Events.Publish(new SkillPreviewRequestEvent(@event.Entity, skill, false));
        }
    }

    private void ExecuteSkill(EntityId caster, SkillDefinitionSO skill)
    {
        if (!_world.Components.TryGet(caster, out CombatStateComponent state))
        {
            return;
        }

        state.CurrentState = CombatState.CastingSkill;
        state.LastActionTime = Time.time;

        _world.Events.Publish(new SkillConfirmExecutionEvent(caster, skill, Vector3.zero, Vector3.forward));
    }

    public void Shutdown() { }
}
