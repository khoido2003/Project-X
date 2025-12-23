using UnityEngine;

public struct AttackStartedEvent
{
    public EntityId Attacker;
    public int AnimationIndex;

    public AttackStartedEvent(EntityId attacker, int animationIndex)
    {
        Attacker = attacker;
        AnimationIndex = animationIndex;
    }
}

public struct AttackHitEvent
{
    public EntityId Entity;

    public AttackHitEvent(EntityId entity)
    {
        Entity = entity;
    }
}

public struct AttackExecutionRequestEvent
{
    public EntityId Attacker;
    public AttackExecutionType Type;
    public Vector3 Direction;
    public float Range;
    public float Damage;

    public ParticleSystem ImpactEffect;

    public GameObject ProjectilePrefab;
    public float ProjectileSpeed;
    public float ProjectileLifetime;
    public Vector3 SpawnOffset;

    public float AreaRadius;
    public float AreaDuration;
}

/// <summary>
/// Published when an attack successfully hits a target
/// Used by Cloak Strike to trigger empowered damage
/// </summary>
public struct AttackPerformedEvent
{
    public EntityId Attacker;
    public EntityId Target;
    public float BaseDamage;
    
    public AttackPerformedEvent(EntityId attacker, EntityId target, float baseDamage)
    {
        Attacker = attacker;
        Target = target;
        BaseDamage = baseDamage;
    }
}

/// <summary>
/// Published to modify damage dealt (e.g., Cloak Strike bonus damage)
/// Damage systems should listen for this and apply the multiplier
/// </summary>
public struct DamageModifierEvent
{
    public EntityId Target;
    public EntityId Attacker;
    public float Multiplier;
    public string Source; // For debugging/logging
}

