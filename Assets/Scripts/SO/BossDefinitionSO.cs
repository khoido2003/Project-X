using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "AI/Boss Definition")]
public class BossDefinitionSO : ScriptableObject
{
    [Header("Prefab")]
    public GameObject prefab;

    [Header("Base Stats")]
    public string bossName = "Corrupted Mech";
    public float maxHealth = 2000f;
    public float moveSpeed = 4f;
    public float enrageHealthThreshold = 0.3f; // Enrage at 30% HP

    [Header("Hammer Attack (Main Weapon)")]
    public float hammerDamage = 40f;
    public float hammerCooldown = 1.5f;
    public float hammerRange = 3f;
    public string hammerAnimationTrigger = "attack";
    public int totalHammerAnimations = 2;
    public ParticleSystem hammerImpactVFX;
    public AudioClip hammerSwingSound;

    [Header("Hammer Slam (Special)")]
    public float hammerSlamDamage = 80f;
    public float hammerSlamRadius = 5f;
    public float hammerSlamCooldown = 8f;
    public string hammerSlamTrigger = "hammerSlam";
    public ParticleSystem hammerSlamVFX;
    public AudioClip hammerSlamSound;

    [Header("Jump Attack")]
    public float jumpAttackRange = 12f;
    public float jumpAttackMinRange = 5f;
    public float jumpAttackCooldown = 6f;
    public float jumpAttackDamage = 50f;
    public float jumpAttackRadius = 4f;
    public float jumpDuration = 0.6f;
    public string jumpAnimationTrigger = "jumpAttack";
    public ParticleSystem jumpLandingVFX;
    public AudioClip jumpSound;

    [Header("Flamethrower Skill")]
    public float flamethrowerCooldown = 10f;
    public float flamethrowerDamagePerTick = 15f;
    public float flamethrowerTickInterval = 0.3f;
    public float flamethrowerRange = 8f;
    public float flamethrowerAngle = 45f;
    public float flamethrowerDuration = 3f;
    public string flamethrowerTrigger = "flamethrower";
    public ParticleSystem flamethrowerVFX;
    public AudioClip flamethrowerSound;

    [Header("Animation")]
    public string isMovingParam = "isMoving";
    public string isRunningParam = "isRunning";
    public string moveXParam = "moveX";
    public string moveYParam = "moveY";

    [Header("Vision")]
    public float detectionRange = 20f;
    public float loseTargetRange = 30f;
    public float fieldOfView = 360f; // Boss sees all directions
    public float checkInterval = 0.2f;
    public LayerMask detectionMask;

    [Header("Audio")]
    public AudioProfileSO audioProfile;
    public AudioClip enrageRoarSound;
    public AudioClip deathSound;
}
