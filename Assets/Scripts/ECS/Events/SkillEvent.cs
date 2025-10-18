using UnityEngine;

public struct SkillExecutionRequestEvent
{
    public EntityId Caster;

    public SkillExecutionRequestEvent(EntityId caster)
    {
        Caster = caster;
    }
}

public struct SkillPreviewRequestEvent
{
    public EntityId Entity;
    public bool IsActive;
    public SkillDefinitionSO Skill;

    public SkillPreviewRequestEvent(EntityId entity, SkillDefinitionSO skill, bool isActive)
    {
        Entity = entity;
        Skill = skill;
        IsActive = isActive;
    }
}

public struct SkillConfirmExecutionEvent
{
    public EntityId Caster;
    public SkillDefinitionSO Skill;
    public Vector3 TargetPoint;
    public Vector3 Direction;

    public SkillConfirmExecutionEvent(EntityId caster, SkillDefinitionSO skill, Vector3 targetPoint, Vector3 direction)
    {
        Caster = caster;
        Skill = skill;
        TargetPoint = targetPoint;
        Direction = direction;
    }
}
