using UnityEngine;

public enum BuffType
{
    DefenseBoost,
    AttackBoost,
    SpeedBoost,
}

public struct DamageEvent
{
    public EntityId Target;
    public EntityId Attacker;
    public float Amount;

    public DamageEvent(EntityId target, EntityId attacker, float amount)
    {
        Target = target;
        Attacker = attacker;
        Amount = amount;
    }
}

public struct KnockbackEvent
{
    public EntityId Target;
    public Vector3 Direction;
    public float Force;
}

public struct StunEvent
{
    public EntityId Target;
    public float Duration;
}

public struct ApplyBuffEvent
{
    public EntityId Target;
    public BuffType BuffType;
    public float Value;
    public float Duration;
}

public struct InvincibilityStartEvent
{
    public EntityId Entity;
    public float Duration;

    public InvincibilityStartEvent(EntityId entity, float duration)
    {
        Entity = entity;
        Duration = duration;
    }
}

public struct InvincibilityEndEvent
{
    public EntityId Entity;

    public InvincibilityEndEvent(EntityId entity)
    {
        Entity = entity;
    }
}
