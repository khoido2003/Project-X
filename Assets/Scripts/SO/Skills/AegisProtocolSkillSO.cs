using UnityEngine;

[CreateAssetMenu(fileName = "AegisProtocolSkill", menuName = "Skills/AegisProtocolSkill")]
public class AegisProtocolSkillSO : SkillDefinitionSO
{
    [Header("Mech Settings")]
    [Tooltip("Duration of mech transformation")]
    public float mechDuration = 10f;

    [Tooltip("Bonus health added as a shield (0.5 = 50% of max health)")]
    [Range(0f, 1f)]
    public float healthBoostPercent = 0.5f;

    [Tooltip("Damage increase while in mech (0.25 = 25%)")]
    [Range(0f, 1f)]
    public float damageBoostPercent = 0.25f;

    [Tooltip("Movement speed reduction while in mech (0.2 = 20% slower)")]
    [Range(0f, 0.5f)]
    public float moveSpeedPenalty = 0.2f;

    [Tooltip("Whether the mech is immune to knockback and stuns")]
    public bool knockbackImmune = true;

    [Header("Mech Visuals")]
    [Tooltip("The mech suit model to show during transformation (child of character)")]
    public GameObject mechModelPrefab;

    [Header("Mech Attack")]
    [Tooltip("Attack animation trigger for mech attacks")]
    public string mechAttackTrigger = "mechAttack";

    [Tooltip("Damage multiplier for mech slam attacks")]
    [Range(1f, 3f)]
    public float mechAttackMultiplier = 1.5f;

    [Header("Transformation VFX/SFX")]
    [Tooltip("VFX played when entering mech")]
    public ParticleSystem enterMechVfxPrefab;

    [Tooltip("VFX played when exiting mech")]
    public ParticleSystem exitMechVfxPrefab;

    [Tooltip("Looping VFX while in mech (e.g., energy effects)")]
    public ParticleSystem activeMechVfxPrefab;

    [Tooltip("Sound played when entering mech")]
    public AudioClip enterMechSound;

    [Tooltip("Sound played when exiting mech")]
    public AudioClip exitMechSound;

    [Tooltip("Looping sound while in mech")]
    public AudioClip mechActiveLoopSound;
}
