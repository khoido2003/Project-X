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
}
