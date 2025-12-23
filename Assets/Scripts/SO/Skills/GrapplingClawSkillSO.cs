using UnityEngine;

[CreateAssetMenu(fileName = "GrapplingClawSkill", menuName = "Skills/GrapplingClawSkill")]
public class GrapplingClawSkillSO : SkillDefinitionSO
{
    [Header("Grapple Settings")]
    [Tooltip("Maximum range to grapple an enemy")]
    public float grappleRange = 10f;
    
    [Tooltip("Speed at which player travels to target")]
    public float grappleSpeed = 25f;
    
    [Tooltip("Damage dealt on impact")]
    public float impactDamage = 20f;
    
    [Tooltip("Duration of stun applied to target on arrival")]
    public float stunDuration = 0.5f;
    
    [Tooltip("VFX for the grapple line/hook")]
    public GameObject grappleLinePrefab;
    
    [Tooltip("VFX played on impact")]
    public ParticleSystem impactVfxPrefab;
    
    [Tooltip("Sound played when grapple fires")]
    public AudioClip grappleFireSound;
    
    [Tooltip("Sound played on impact")]
    public AudioClip impactSound;
}
