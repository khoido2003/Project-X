using System;
using UnityEngine;

[Serializable]
public class SkillInstance
{
    public SkillData Data { get; private set; }
    private float lastUsedTime = -Mathf.Infinity;

    public SkillInstance(SkillData data)
    {
        Data = data;
    }

    public bool CanUse()
    {
        return Time.time >= lastUsedTime + Data.coolDown;
    }

    public void Use(GameObject owner, Vector3 targetPoint, Vector3 direction)
    {
        // Cast skill
        Data.Execute(owner, targetPoint, direction);

        lastUsedTime = Time.time;
    }
}
