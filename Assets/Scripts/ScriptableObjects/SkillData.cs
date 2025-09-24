using UnityEngine;

public abstract class SkillData : ScriptableObject
{
    public string skillName;

    public float coolDown = 5f;

    public float castRange = 5f;

    public AudioClip activateSound;
    public string activationAnimationTrigger = "skill";

    public abstract void Execute(GameObject owner, Vector3 targetPoint, Vector3 direction);
}
