using UnityEngine;

public class HealthSystem : ISystem
{
    private World _world;

    public void Initialize(World world)
    {
        _world = world;

        foreach (var (entity, health) in _world.Components.Query<HealthDataComponent>())
        {
            world.Events.Publish(new HealthChangedEvent(entity, health.CurrentHealth, health.MaxHealth));
        }
    }

    public void Update(float dt)
    {
        foreach (var (entity, health) in _world.Components.Query<HealthDataComponent>())
        {
            if (health.IsDead)
            {
                continue;
            }

            // Health check
            if (health.CurrentHealth <= 0f)
            {
                health.CurrentHealth = 0f;
                health.IsDead = true;

                HandleEntityDeath(entity);
            }
        }
    }

    private void HandleEntityDeath(EntityId entity)
    {
        // Try to identify what kind of entity this is
        if (_world.Components.Has<EnemyComponent>(entity))
        {
            _world.Events.Publish(new EntityDeathEvent(entity));

            // Switch AI to Dead state
            EnemyAIHelpers.ChangeState(_world, entity, EnemyState.Dead);
        }
        else if (_world.Components.Has<PlayerTagComponent>(entity))
        {
            // Handle player death differently
            _world.Events.Publish(new EntityDeathEvent(entity));
            Debug.Log("Player has died!");
        }
    }

    public void FixedUpdate(float dt) { }

    public void Shutdown() { }
}
