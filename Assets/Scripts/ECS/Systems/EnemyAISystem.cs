using System;
using Unity.Netcode;
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
        if (!NetworkManager.Singleton.IsServer)
        {
            return;
        }

        int frameIndex = Time.frameCount % 3;
        
        foreach (var (entity, ai) in _world.Components.Query<EnemyComponent>())
        {
            // OPTIMIZATION: Stagger updates - only 1/3 of enemies update each frame
            // Boss always updates for responsiveness, others are staggered by entity ID
            if (!ai.IsBoss && (entity.Id % 3) != frameIndex)
            {
                continue;
            }
            
            // Multiply dt by 3 to compensate for updating every 3rd frame
            float effectiveDt = ai.IsBoss ? dt : dt * 3f;
            ai.StateTime += effectiveDt;

            IEnemyState state = EnemyAIHelpers.GetState(ai.CurrentState);

            if (state != null)
            {
                try
                {
                    state.OnUpdate(_world, entity, effectiveDt);
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }
            else
            {
                Debug.LogError($"[EnemyAISystem] Entity {entity.Id}: No state implementation for {ai.CurrentState}!");
            }
        }
    }

    public void FixedUpdate(float dt) { }

    private void OnPlayerDetectedEvent(EnemyPlayerDetectedEvent @event)
    {
        if (!NetworkManager.Singleton.IsServer)
        {
            return;
        }

        if (!IsValidEntity(@event.Enemy) || !IsValidEntity(@event.Player))
        {
            return;
        }

        EnemyComponent enemy = _world.Components.Get<EnemyComponent>(@event.Enemy);
        enemy.TargetEntity = @event.Player;

        if (enemy.CurrentState != EnemyState.Chase)
        {
            EnemyAIHelpers.ChangeState(_world, @event.Enemy, EnemyState.Chase);
        }
    }

    private void OnEnemyPlayerLostEvent(EnemyPlayerLostEvent @event)
    {
        if (!NetworkManager.Singleton.IsServer)
        {
            return;
        }

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
