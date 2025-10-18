using UnityEngine;

public enum AnimationParameterType
{
    Trigger,
    Bool,
    Float,
    Int,
}

public struct AnimationParameterEvent
{
    public EntityId Entity;
    public string ParameterName;
    public AnimationParameterType ParameterType;
    public object Value;

    public AnimationParameterEvent(EntityId entity, string name, AnimationParameterType type, object value)
    {
        Entity = entity;
        ParameterName = name;
        ParameterType = type;
        Value = value;
    }
}

/////////////////////////////

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
