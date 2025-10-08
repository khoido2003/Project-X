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
