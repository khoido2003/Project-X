using System.Collections.Generic;
using UnityEngine;

public class SkillSetComponent
{
    public List<SkillDefinitionSO> Skills;
    public float[] CooldownUntil;

    public SkillSetComponent(List<SkillDefinitionSO> skills)
    {
        // Create a NEW list to avoid mutating the original ScriptableObject's skills list
        Skills = new List<SkillDefinitionSO>(skills);
        CooldownUntil = new float[skills.Count];
    }
}
