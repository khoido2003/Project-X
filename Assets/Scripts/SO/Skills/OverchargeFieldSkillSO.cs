using UnityEngine;

[CreateAssetMenu(fileName = "OverchargeFieldSkill", menuName = "Skills/OverchargeFieldSkill")]
public class OverchargeFieldSkillSO : SkillDefinitionSO
{
    [Header("Field Settings")]
    [Tooltip("Duration the field remains active")]
    public float fieldDuration = 5f;

    [Tooltip("Radius of the field effect")]
    public float fieldRadius = 4f;

    [Header("Ally Buffs")]
    [Tooltip("Attack speed increase for allies in the field (0.3 = 30%)")]
    [Range(0f, 1f)]
    public float attackSpeedBoost = 0.3f;

    [Tooltip("Damage increase for allies in the field (0.15 = 15%)")]
    [Range(0f, 1f)]
    public float damageBoost = 0.15f;

    [Header("Enemy Debuffs")]
    [Tooltip("Movement slow applied to enemies in the field (0.2 = 20% slow)")]
    [Range(0f, 0.8f)]
    public float enemySlowPercent = 0.2f;

    [Header("Field VFX/SFX")]
    [Tooltip("VFX prefab for the electric field effect")]
    public ParticleSystem fieldVfxPrefab;

    [Tooltip("VFX played on allies receiving buff")]
    public ParticleSystem buffVfxPrefab;

    [Tooltip("Sound played when field activates")]
    public AudioClip activateSound;

    [Tooltip("Looping sound while field is active")]
    public AudioClip activeLoopSound;

    [Tooltip("Sound played when field expires")]
    public AudioClip deactivateSound;
}
