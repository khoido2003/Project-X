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
            Debug.LogWarning($"[SkillSystem] Entity {@event.Entity.Id} has no SkillSetComponent!");
            return;
        }

        int index = @event.SkillIndex - 1;
        if (index < 0 || index >= skillSet.Skills.Count)
        {
            Debug.LogWarning($"[SkillSystem] Invalid skill index {index} for entity {@event.Entity.Id} (Skills.Count={skillSet.Skills.Count})");
            return;
        }

        // Check cooldown on BOTH server and client - client has local cooldown tracking
        if (Time.time < skillSet.CooldownUntil[index])
        {
            return;
        }

        SkillDefinitionSO skill = skillSet.Skills[index];
        if (skill == null)
        {
            Debug.LogWarning($"[SkillSystem] Skill at index {index} is null for entity {@event.Entity.Id}");
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
                // For instant skills, set up the buffer with current direction and send RPC
                if (!_world.Components.TryGet(@event.Entity, out SkillCastBufferComponent buffer))
                {
                    buffer = new SkillCastBufferComponent();
                    _world.Components.Add(@event.Entity, buffer);
                }
                
                buffer.Skill = skill;
                
                // Get mouse position for direction
                Vector3 targetPoint = Vector3.zero;
                Vector3 direction = Vector3.forward;
                
                var registry = _world.Services.Resolve<EntityViewRegistry>();
                if (registry.TryGet(@event.Entity, out EntityView view))
                {
                    direction = view.transform.forward;
                }
                
                buffer.TargetPoint = targetPoint;
                buffer.Direction = direction;
                
                if (NetworkManager.Singleton.IsServer)
                {
                    // Server executes directly
                    ExecuteSkill(@event.Entity, currentChosenSkill);
                }
                else
                {
                    // Client sends RPC to server for instant skills - include skill index to fix race condition
                    if (_world.Components.TryGet(@event.Entity, out NetworkSyncComponent sync))
                    {
                        Debug.Log($"[SkillSystem] Client requesting instant skill {skill.skillName} execution via RPC (index: {index})");
                        sync.SyncView.RequestSkillExecutionServerRpc(targetPoint, direction, index);
                    }
                }
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

        // Use buffered target/direction if available (set by client request)
        Vector3 targetPoint = Vector3.zero;
        Vector3 direction = Vector3.forward;

        if (_world.Components.TryGet(caster, out SkillCastBufferComponent buffer))
        {
            targetPoint = buffer.TargetPoint;
            direction = buffer.Direction;
        }

        // Ensure a valid horizontal direction
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f && _world.Components.TryGet(caster, out TransformComponent trans))
        {
            direction = trans.Rotation * Vector3.forward;
        }
        if (direction.sqrMagnitude < 0.0001f)
        {
            direction = Vector3.forward;
        }

        // CRITICAL: Publish SkillEffectTriggerEvent to apply cooldown (otherwise instant skills have no cooldown!)
        _world.Events.Publish(
            new SkillEffectTriggerEvent
            {
                Caster = caster,
                Skill = skill,
                TargetPoint = targetPoint,
                Direction = direction.normalized
            }
        );
        
        _world.Events.Publish(new SkillConfirmExecutionEvent(caster, skill, targetPoint, direction.normalized));
    }

    private void OnAnimationRelayEvent(AnimationEventRelayEvent @event)
    {
        if (@event.EventType == AnimationEventRelayType.SKILL_END)
        {
            _world.Events.Publish(new ExitCombatStateEvent { Entity = @event.Entity, TargetState = CombatState.Idle });
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
        // Apply cooldown on BOTH server and client for proper local tracking
        if (_world.Components.TryGet(@event.Caster, out SkillSetComponent skillSet))
        {
            for (int i = 0; i < skillSet.Skills.Count; i++)
            {
                if (skillSet.Skills[i] == @event.Skill)
                {
                    skillSet.CooldownUntil[i] = Time.time + @event.Skill.cooldown;
                    Debug.Log(
                        $"[SkillSystem] Skill {@event.Skill.skillName} on cooldown until {skillSet.CooldownUntil[i]} (IsServer: {NetworkManager.Singleton.IsServer})"
                    );
                    break;
                }
            }
        }

        // Server-only: additional state management
        if (!NetworkManager.Singleton.IsServer)
        {
            return;
        }

        // Clear SkillPreview flag to allow attacks
        if (_world.Components.TryGet(@event.Caster, out ActionFlagComponent flags))
        {
            flags.Set(ActionFlag.SkillPreview, false);
        }

        // Force reset combat state immediately
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
