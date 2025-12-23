using System;
using System.Collections.Generic;
using UnityEngine;

public static class EnemyAIHelpers
{
    private static readonly Dictionary<EnemyState, IEnemyState> _registry = new();

    public static void RegisterState(IEnemyState state)
    {
        if (state == null)
        {
            throw new ArgumentException(nameof(state));
        }
        _registry[state.StateType] = state;
    }

    public static bool HasState(EnemyState state) => _registry.ContainsKey(state);

    public static IEnemyState GetState(EnemyState state)
    {
        if (_registry.TryGetValue(state, out IEnemyState s))
        {
            return s;
        }

        return null;
    }

    public static void RegisterDefaultStates()
    {
        if (_registry.Count > 0)
        {
            return;
        }

        RegisterState(new EnemyIdleStateAI());
        RegisterState(new EnemyPatrolStateAI());
        RegisterState(new EnemyChaseStateAI());
        RegisterState(new EnemyAttackStateAI());

        RegisterState(new BossJumpAttackStateAI());
        RegisterState(new BossFlamethrowerStateAI());

        RegisterState(new EnemyDeadStateAI());
        RegisterState(new EnemyTakeCoverStateAI());
    }

    public static void ChangeState(World world, EntityId entity, EnemyState newState)
    {
        if (!world.Components.TryGet(entity, out EnemyComponent enemy))
        {
            Debug.LogError($"ChangeState called for entity {entity} without EnemyAIComponent");
            return;
        }

        if (enemy.CurrentState == newState)
        {
            return;
        }

        IEnemyState oldState = GetState(enemy.CurrentState);
        try
        {
            oldState?.OnExit(world, entity);
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }

        enemy.CurrentState = newState;
        enemy.StateTime = 0f;

        IEnemyState newStateImpl = GetState(newState);
        if (newStateImpl != null)
        {
            try
            {
                newStateImpl.OnEnter(world, entity);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }
        else
        {
            Debug.LogError($"No registered iEnemyState for {newState}");
        }
    }
}
