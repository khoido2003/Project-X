using System;
using UnityEngine;

public class SkillSystem : ISystem
{
    public World _world;
    private EntityViewRegistry _registry;

    public void Initialize(World world)
    {
        _world = world;
        _registry = world.Services.Resolve<EntityViewRegistry>();

        _world.Events.Subscribe<SkillInputEvent>(OnSkillInput);
        _world.Events.Subscribe<SkillExecutionRequestEvent>(OnExecuteSkill);
    }

    public void Update(float dt) { }

    public void FixedUpdate(float dt) { }

    private void OnExecuteSkill(SkillExecutionRequestEvent @event)
    {
        if (!_world.Components.TryGet(@event.Caster, out SkillSetComponent skillSet))
        {
            return;
        }

        int index = skillSet.Skills.IndexOf(@event.Skill);
        if (index < 0)
        {
            return;
        }

        if (Time.time < skillSet.CooldownUntil[index])
        {
            return;
        }

        skillSet.CooldownUntil[index] = Time.time + @event.Skill.cooldown;

        if (_world.Components.TryGet(@event.Caster, out CombatStateComponent state))
        {
            state.CurrentState = CombatState.Idle;
        }
    }

    private void OnSkillInput(SkillInputEvent @event)
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

        SkillDefinitionSO skill = skillSet.Skills[index];
        if (skill == null)
        {
            return;
        }

        if (!_world.Components.TryGet(@event.Entity, out CombatStateComponent state))
        {
            state = new CombatStateComponent { CurrentState = CombatState.Idle };
            _world.Components.Add(@event.Entity, state);
        }

        if (state.CurrentState != CombatState.Idle)
        {
            return;
        }

        if (_registry.TryGet(@event.Entity, out EntityView view) && view.TryGetComponent(out SkillPreviewView preview))
        {
            if (@event.IsPressed)
            {
                if (skill.isInstant)
                {
                    ExecuteSkill(@event.Entity, skill);
                }
                else
                {
                    preview.ShowPreview(skill);
                }
            }
            else
            {
                preview.HidePreview();
            }
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

        _world.Events.Publish(
            new SkillExecutionRequestEvent
            {
                Caster = caster,

                Skill = skill,
                TargetPoint = Vector3.zero,
                Direction = Vector3.forward,
            }
        );
    }

    public void Shutdown() { }
}
