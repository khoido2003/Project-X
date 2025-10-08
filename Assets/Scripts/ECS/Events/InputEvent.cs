using UnityEngine;

public struct MoveInputEvent
{
    public EntityId Entity;
    public Vector2 Input;

    public MoveInputEvent(EntityId entity, Vector2 input)
    {
        Entity = entity;
        Input = input;
    }
}

public struct AttackInputEvent
{
    public EntityId Entity;

    public AttackInputEvent(EntityId entity)
    {
        Entity = entity;
    }
}

public struct SkillInputEvent
{
    public EntityId Entity;
    public int SkillIndex;
    public bool IsPressed;

    public SkillInputEvent(EntityId entity, int skillIndex, bool isPressed)
    {
        Entity = entity;
        SkillIndex = skillIndex;
        IsPressed = isPressed;
    }
}
