using UnityEngine;

public enum SkillCategory
{
    // Cipher
    DashStrike,
    HomerunSwing,
    PlasmaShield,

    // Daisy
    RapidFire,
    ExplosiveShot,
    SniperShot,
    
    // Murder Kitten
    CloakStrike,
    GrapplingClaw,
    BladeStorm,
}

public abstract class SkillDefinitionSO : NetworkSO
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
