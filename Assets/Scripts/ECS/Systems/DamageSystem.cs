using System;
using UnityEngine;

public class DamageSystem : ISystem
{
    private World _world;

    public void Initialize(World world)
    {
        _world = world;
        _world.Events.Subscribe<DamageEvent>(OnDamage);
    }

    public void Update(float dt) { }

    public void FixedUpdate(float dt) { }

    private void OnDamage(DamageEvent @event)
    {
        if (!_world.Components.TryGet(@event.Target, out HealthDataComponent health))
        {
            Debug.LogWarning($"[DamageSystem] Target {@event.Target} missing HealthDataComponent");
            return;
        }

        if (health.IsDead)
        {
            return;
        }

        health.CurrentHealth -= @event.Amount;

        if (health.CurrentHealth < 0)
        {
            health.CurrentHealth = 0;
        }

        _world.Components.Add(@event.Target, health);

        _world.Events.Publish(new HealthChangedEvent(@event.Target, health.CurrentHealth, health.MaxHealth));
    }

    public void Shutdown()
    {
        _world.Events.Unsubscribe<DamageEvent>(OnDamage);
    }
}
