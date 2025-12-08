using System;
using Unity.Netcode;
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
        _world.Events.Subscribe<AnimationEventRelayEvent>(OnAnimationRelayEvent);
        _world.Events.Subscribe<SkillExecutionFinishedEvent>(OnSkillExecutionFinishedEvent);
        _world.Events.Subscribe<SkillEffectTriggerEvent>(OnSkillEffectTrigger);
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

        if (NetworkManager.Singleton.IsServer && Time.time < skillSet.CooldownUntil[index])
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
        if (!NetworkManager.Singleton.IsServer)
        {
            return;
        }

        _world.Events.Publish(new EnterCombatStateEvent { Entity = caster, TargetState = CombatState.CastingSkill });

        _world.Events.Publish(new SkillConfirmExecutionEvent(caster, skill, Vector3.zero, Vector3.forward));
    }

    private void OnAnimationRelayEvent(AnimationEventRelayEvent @event)
    {
        if (@event.EventType == AnimationEventRelayType.SKILL_END)
        {
            _world.Events.Publish(
                new ExitCombatStateEvent { Entity = @event.Entity, TargetState = CombatState.CastingSkill }
            );
        }
    }

    private void OnSkillExecutionFinishedEvent(SkillExecutionFinishedEvent @event)
    {
        _world.Events.Publish(
            new ExitCombatStateEvent { Entity = @event.Caster, TargetState = CombatState.CastingSkill }
        );
    }

    private void OnSkillEffectTrigger(SkillEffectTriggerEvent @event)
    {
        // Server applies cooldown
        if (!NetworkManager.Singleton.IsServer)
        {
            return;
        }

        if (_world.Components.TryGet(@event.Caster, out SkillSetComponent skillSet))
        {
            for (int i = 0; i < skillSet.Skills.Count; i++)
            {
                if (skillSet.Skills[i] == @event.Skill)
                {
                    skillSet.CooldownUntil[i] = Time.time + @event.Skill.cooldown;
                    Debug.Log(
                        $"[SkillSystem] Skill {@event.Skill.skillName} on cooldown until {skillSet.CooldownUntil[i]}"
                    );

                    // Broadcast skill effect to all clients
                    if (_world.Components.TryGet(@event.Caster, out NetworkSyncComponent sync))
                    {
                        sync.SyncView.BroadcastSkillEffectClientRpc(
                            @event.Skill.category,
                            @event.TargetPoint,
                            @event.Direction
                        );
                    }

                    break;
                }
            }
        }

        // Ensure we return to idle so the player can attack again after skill
        // Force reset combat state immediately - this is the primary fix
        if (_world.Components.TryGet(@event.Caster, out CombatStateComponent combat))
        {
            combat.CurrentState = CombatState.Idle;
            combat.LastActionTime = Time.time;
            
            // Publish state change event to notify other systems
            _world.Events.Publish(
                new CombatStateChangedEvent
                {
                    Entity = @event.Caster,
                    Previous = CombatState.CastingSkill,
                    Current = CombatState.Idle,
                }
            );
        }
        
        // Also publish exit event for consistency
        _world.Events.Publish(
            new ExitCombatStateEvent { Entity = @event.Caster, TargetState = CombatState.CastingSkill }
        );
    }

    public void Shutdown() { }
}
