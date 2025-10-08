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
    public EntityId Attacker;
    public Vector3 HitDirection;

    public AttackHitEvent(EntityId attacker, Vector3 hitDirection)
    {
        Attacker = attacker;
        HitDirection = hitDirection;
    }
}
