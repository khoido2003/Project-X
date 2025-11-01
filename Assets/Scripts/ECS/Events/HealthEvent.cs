using UnityEngine;

public struct HealthChangedEvent
{
    public EntityId Entity;
    public float CurrentHealth;
    public float MaxHealth;

    public HealthChangedEvent(EntityId entity, float currentHealth, float maxHealth)
    {
        Entity = entity;
        CurrentHealth = currentHealth;
        MaxHealth = maxHealth;
    }
}

public struct EntityDeathEvent
{
    public EntityId Entity;

    public EntityDeathEvent(EntityId entity)
    {
        Entity = entity;
    }
}
