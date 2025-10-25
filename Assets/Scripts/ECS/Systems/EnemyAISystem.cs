using System;
using UnityEngine;

public class EnemyAISystem : ISystem
{
    private World _world;

    public void Initialize(World world)
    {
        _world = world;

        _world.Events.Subscribe<EnemyPlayerDetectedEvent>(OnPlayerDetectedEvent);

        _world.Events.Subscribe<EnemyPlayerLostEvent>(OnEnemyPlayerLostEvent);
    }

    public void Update(float dt)
    {
        foreach (var (entity, ai) in _world.Components.Query<EnemyComponent>())
        {
            ai.StateTime += dt;

            IEnemyState state = EnemyAIHelpers.GetState(ai.CurrentState);

            if (state != null)
            {
                try
                {
                    state.OnUpdate(_world, entity, dt);
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }
        }
    }

    public void FixedUpdate(float dt) { }

    private void OnPlayerDetectedEvent(EnemyPlayerDetectedEvent @event)
    {
        if (!IsValidEntity(@event.Enemy) || !IsValidEntity(@event.Player))
        {
            return;
        }

        EnemyComponent ai = _world.Components.Get<EnemyComponent>(@event.Enemy);
        ai.TargetEntity = @event.Player;

        EnemyAIHelpers.ChangeState(_world, @event.Enemy, EnemyState.Chase);
    }

    private void OnEnemyPlayerLostEvent(EnemyPlayerLostEvent @event)
    {
        if (!IsValidEntity(@event.Enemy))
        {
            return;
        }

        EnemyComponent ai = _world.Components.Get<EnemyComponent>(@event.Enemy);

        if (ai.TargetEntity == @event.Player)
        {
            ai.TargetEntity = default;
        }

        EnemyAIHelpers.ChangeState(_world, @event.Enemy, EnemyState.Patrol);
    }

    private bool IsValidEntity(EntityId entity) => _world.Entities.Exists(entity);

    public void Shutdown() { }
}
