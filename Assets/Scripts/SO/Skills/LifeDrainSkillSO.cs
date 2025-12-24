using UnityEngine;

[CreateAssetMenu(fileName = "LifeDrainSkill", menuName = "Skills/LifeDrainSkill")]
public class LifeDrainSkillSO : SkillDefinitionSO
{
    [Header("Life Drain Settings")]
    [Tooltip("Percentage of damage dealt that heals the caster (0-1)")]
    public float lifestealPercent = 0.5f;
    
    [Tooltip("Radius of the drain effect")]
    public float drainRadius = 4f;
    
    [Tooltip("Duration of the draining effect")]
    public float drainDuration = 2f;
    
    [Tooltip("Number of damage ticks during drain")]
    public int tickCount = 4;
    
    [Tooltip("VFX for the drain effect around caster")]
    public ParticleSystem drainVfxPrefab;
    
    [Tooltip("VFX spawned on enemies being drained")]
    public ParticleSystem enemyDrainVfxPrefab;
    
    [Tooltip("Sound played during drain")]
    public AudioClip drainLoopSound;
    
    [Tooltip("Heal VFX prefab")]
    public ParticleSystem healVfxPrefab;
}
