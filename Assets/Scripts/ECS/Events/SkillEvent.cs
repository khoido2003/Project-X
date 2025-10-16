using UnityEngine;

public struct SkillExecutionRequestEvent
{
    public EntityId Caster;
    public SkillDefinitionSO Skill;
    public Vector3 TargetPoint;
    public Vector3 Direction;
}

public struct SkillPreviewRequestEvent
{
    public EntityId Entity;
    public SkillDefinitionSO Skill;
    public bool Show;
}
