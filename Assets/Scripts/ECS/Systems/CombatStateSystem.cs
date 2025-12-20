using System;
using UnityEngine;

public class CombatStateSystem : ISystem
{
    private World _world;
    private const float maxStateTime = 5f;

    public void Initialize(World world)
    {
        _world = world;
        _world.Events.Subscribe<EnterCombatStateEvent>(OnEnterCombatStateEvent);

        _world.Events.Subscribe<ExitCombatStateEvent>(OnExitCombateStatEvent);
    }

    public void Update(float dt)
    {
        foreach (var (entity, state) in _world.Components.Query<CombatStateComponent>())
        {
            if (state.CurrentState != CombatState.Idle && Time.time - state.LastActionTime > maxStateTime)
            {
                SetState(entity, CombatState.Idle);
            }
        }
    }

    public void FixedUpdate(float dt) { }

    private void OnEnterCombatStateEvent(EnterCombatStateEvent @event)
    {
        if (!_world.Components.TryGet(@event.Entity, out CombatStateComponent state))
        {
            state = new CombatStateComponent();
            _world.Components.Add(@event.Entity, state);
        }

        if (state.CurrentState != @event.TargetState)
        {
            var prev = state.CurrentState;
            state.CurrentState = @event.TargetState;
            state.LastActionTime = Time.time;

            _world.Events.Publish(
                new CombatStateChangedEvent
                {
                    Entity = @event.Entity,
                    Previous = prev,
                    Current = @event.TargetState,
                }
            );
        }
    }

    private void OnExitCombateStatEvent(ExitCombatStateEvent @event)
    {
        if (!_world.Components.TryGet(@event.Entity, out CombatStateComponent state))
        {
            return;
        }

        // Always reset to Idle when exiting CastingSkill or Attacking state
        // This ensures attacks/skills don't permanently block subsequent actions
        // The client may be in a different state than the server due to network timing,
        // so we reset regardless of current state for these critical transitions
        if (@event.TargetState == CombatState.CastingSkill || @event.TargetState == CombatState.Attacking)
        {
            SetState(@event.Entity, CombatState.Idle);
        }
        // For other states, only reset if we're currently in that state
        else if (state.CurrentState == @event.TargetState)
        {
            SetState(@event.Entity, CombatState.Idle);
        }
    }

    private void SetState(EntityId entity, CombatState newState)
    {
        var state = _world.Components.Get<CombatStateComponent>(entity);

        CombatState prev = state.CurrentState;
        state.CurrentState = newState;
        state.LastActionTime = Time.time;

        _world.Events.Publish(
            new CombatStateChangedEvent
            {
                Entity = entity,
                Previous = prev,
                Current = newState,
            }
        );
    }

    public void Shutdown() { }
}
