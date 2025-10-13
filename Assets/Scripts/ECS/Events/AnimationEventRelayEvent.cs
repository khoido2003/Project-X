using UnityEngine;

public enum AnimationEventRelayType
{
    ATTACK_HIT,
    SKILL_HIT,
    ATTACK_END,
}

public struct AnimationEventRelayEvent
{
    public EntityId Entity;
    public AnimationEventRelayType EventType;

    public AnimationEventRelayEvent(EntityId entity, AnimationEventRelayType eventType)
    {
        Entity = entity;
        EventType = eventType;
    }
}
