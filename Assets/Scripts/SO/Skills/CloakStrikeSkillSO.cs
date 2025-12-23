using UnityEngine;

[CreateAssetMenu(fileName = "CloakStrikeSkill", menuName = "Skills/CloakStrikeSkill")]
public class CloakStrikeSkillSO : SkillDefinitionSO
{
    [Header("Cloak Settings")]
    [Tooltip("Duration of invisibility in seconds")]
    public float cloakDuration = 3f;
    
    [Tooltip("Damage multiplier for the empowered attack (1.0 = 100% bonus)")]
    public float bonusDamageMultiplier = 1f;
    
    [Tooltip("VFX played when cloaking")]
    public ParticleSystem cloakVfxPrefab;
    
    [Tooltip("VFX played when uncloaking/striking")]
    public ParticleSystem uncloakVfxPrefab;
    
    [Tooltip("Sound played when cloaking")]
    public AudioClip cloakSound;
}
