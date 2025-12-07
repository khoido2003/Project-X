using Unity.Netcode;
using UnityEngine;

public class HealthRegenSystem : ISystem
{
    private World _world;

    public void Initialize(World world)
    {
        _world = world;
    }

    public void Update(float dt)
    {
        if (!NetworkManager.Singleton.IsServer)
        {
            return;
        }

        foreach (
            var (entity, health, upgrades) in _world.Components.Query<HealthDataComponent, PlayerUpgradesComponent>()
        )
        {
            if (health.IsDead || upgrades.HealthRegenPerSecond <= 0f)
            {
                continue;
            }

            float regenAmount = upgrades.HealthRegenPerSecond * dt;

            health.CurrentHealth = Mathf.Min(health.CurrentHealth + regenAmount, health.MaxHealth);

            if (Time.frameCount % 60 == 0)
            {
                _world.Events.Publish(new HealthChangedEvent(entity, health.CurrentHealth, health.MaxHealth));
            }
        }
    }

    public void FixedUpdate(float dt) { }

    public void Shutdown() { }
}
