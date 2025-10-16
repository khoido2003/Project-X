using UnityEngine;

public interface ISkillExecutor
{
    public SkillCategory Category { get; }
    void Execute(World world, SkillExecutionRequestEvent evt);
}
