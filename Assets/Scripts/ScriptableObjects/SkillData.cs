using UnityEngine;

public abstract class SkillData : MonoBehaviour
{
    public string skillName;
    public float coolDown = 5f;
    public AudioClip activateSound;
    public string activationAnimationTrigger = "Skill";

    public abstract void Execute(GameObject owner, Vector3 direction);
}
