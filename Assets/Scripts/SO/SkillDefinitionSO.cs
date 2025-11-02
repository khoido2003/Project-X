using UnityEngine;

public enum SkillCategory
{
    DashStrike,
    HomerunSwing,
    PlasmaShield,
}

public abstract class SkillDefinitionSO : ScriptableObject
{
    public string skillName;
    public SkillCategory category;
    public bool isInstant = false;
    public string keyTrigger = "Q";

    [Header("Base Stats")]
    public float damage;
    public float castRange;
    public float cooldown;

    [Header("Common VFX / SFX")]
    public ParticleSystem hitVfxPrefab;
    public ParticleSystem skillVfxPrefab;
    public AudioClip activateSound;
    public string activationAnimationTrigger = "skill";
}
