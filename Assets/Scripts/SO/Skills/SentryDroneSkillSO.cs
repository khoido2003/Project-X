using UnityEngine;

[CreateAssetMenu(fileName = "SentryDroneSkill", menuName = "Skills/SentryDroneSkill")]
public class SentryDroneSkillSO : SkillDefinitionSO
{
    [Header("Explosive Drone Settings")]
    [Tooltip("Prefab for the drone entity (must have NetworkObject)")]
    public GameObject dronePrefab;

    [Tooltip("Speed at which drone flies toward target")]
    public float flightSpeed = 12f;

    [Tooltip("Maximum lifetime before auto-detonation")]
    public float maxLifetime = 5f;

    [Tooltip("Detection range to find initial target")]
    public float detectionRange = 15f;

    [Header("Explosion Settings")]
    [Tooltip("Radius of explosion damage")]
    public float explosionRadius = 4f;

    [Tooltip("Damage dealt by explosion")]
    public float explosionDamage = 80f;

    [Tooltip("VFX played when drone explodes")]
    public ParticleSystem explosionVfxPrefab;

    [Tooltip("Sound played when drone explodes")]
    public AudioClip explosionSound;

    [Header("Spawn VFX/SFX")]
    [Tooltip("VFX played when drone spawns")]
    public ParticleSystem spawnVfxPrefab;

    [Tooltip("Sound played when drone spawns")]
    public AudioClip spawnSound;

    [Tooltip("Looping sound while drone is flying")]
    public AudioClip flyingLoopSound;
}
