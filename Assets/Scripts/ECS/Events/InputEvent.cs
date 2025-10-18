using UnityEngine;

public struct MovePressedInputEvent
{
    public EntityId Entity;
    public Vector2 Input;

    public MovePressedInputEvent(EntityId entity, Vector2 input)
    {
        Entity = entity;
        Input = input;
    }
}

public struct AttackPressedInputEvent
{
    public EntityId Entity;

    public AttackPressedInputEvent(EntityId entity)
    {
        Entity = entity;
    }
}

public struct SkillPressedInputEvent
{
    public EntityId Entity;
    public int SkillIndex;
    public bool IsPressed;

    public SkillPressedInputEvent(EntityId entity, int skillIndex, bool isPressed)
    {
        Entity = entity;
        SkillIndex = skillIndex;
        IsPressed = isPressed;
    }
}

public struct MouseWorldInputEvent
{
    public EntityId Entity;
    public Vector3 MousePosition;

    public MouseWorldInputEvent(EntityId entity, Vector3 mousePos)
    {
        MousePosition = mousePos;
        Entity = entity;
    }
}
