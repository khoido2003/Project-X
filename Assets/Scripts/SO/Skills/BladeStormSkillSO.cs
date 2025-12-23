using UnityEngine;

[CreateAssetMenu(fileName = "BladeStormSkill", menuName = "Skills/BladeStormSkill")]
public class BladeStormSkillSO : SkillDefinitionSO
{
    [Header("Blade Storm Settings")]
    [Tooltip("Duration of the spinning attack")]
    public float spinDuration = 2f;
    
    [Tooltip("Damage dealt per tick")]
    public float damagePerTick = 10f;
    
    [Tooltip("Time between damage ticks")]
    public float tickInterval = 0.1f;
    
    [Tooltip("Radius of the spinning attack")]
    public float spinRadius = 3f;
    
    [Tooltip("Movement speed multiplier during spin (0.5 = 50% speed)")]
    public float moveSpeedMultiplier = 0.5f;
    
    [Tooltip("Rotation speed during spin (degrees per second)")]
    public float spinRotationSpeed = 1080f;
    
    [Tooltip("VFX for the spinning blades effect")]
    public ParticleSystem spinVfxPrefab;
    
    [Tooltip("Sound played during spin (looping)")]
    public AudioClip spinLoopSound;
    
    [Tooltip("Sound played when spin ends")]
    public AudioClip spinEndSound;
}
