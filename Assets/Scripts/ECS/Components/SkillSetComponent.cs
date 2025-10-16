using System.Collections.Generic;
using UnityEngine;

public class SkillSetComponent
{
    public List<SkillDefinitionSO> Skills;
    public float[] CooldownUntil;

    public SkillSetComponent(List<SkillDefinitionSO> skills)
    {
        Skills = skills;
        CooldownUntil = new float[skills.Count];
    }
}
