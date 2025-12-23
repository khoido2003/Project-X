using UnityEngine;

public class BossComponent
{
    // --- Boss Identity ---
    public string BossName;
    public BossPhase CurrentPhase = BossPhase.Normal;
    public float EnrageHealthThreshold = 0.3f;

    // --- Jump Attack ---
    public float JumpAttackRange = 12f;
    public float JumpAttackMinRange = 5f;
    public float JumpAttackCooldown = 6f;
    public float JumpAttackDamage = 50f;
    public float JumpAttackRadius = 4f;
    public float JumpDuration = 0.6f;
    public float LastJumpTime;
    public bool IsJumping;
    public Vector3 JumpTargetPosition;
    public float JumpProgress;
    public ParticleSystem JumpLandingVFXPrefab; // VFX prefab reference

    // --- Flamethrower Skill ---
    public float FlamethrowerCooldown = 10f;
    public float FlamethrowerDamagePerTick = 15f;
    public float FlamethrowerTickInterval = 0.3f;
    public float FlamethrowerRange = 8f;
    public float FlamethrowerAngle = 45f;
    public float FlamethrowerDuration = 3f;
    public float LastFlamethrowerTime;
    public bool IsFlaming;
    public float FlameProgress;
    public ParticleSystem FlamethrowerVFXPrefab; // VFX prefab reference
    public ParticleSystem ActiveFlameVFX; // Currently active VFX instance

    // --- Hammer Attack ---
    public float HammerSlamCooldown = 8f;
    public float HammerSlamDamage = 80f;
    public float HammerSlamRadius = 5f;
    public float LastHammerSlamTime;
    public ParticleSystem HammerSlamVFXPrefab; // VFX prefab reference
    
    // --- Audio Clips ---
    public AudioClip JumpSound;
    public AudioClip FlamethrowerSound;
    public AudioClip HammerSwingSound;
    public AudioClip HammerSlamSound;

    // --- Target Switching ---
    public float TargetSwitchCooldown = 2f; // Delay before switching to a new target
    public float LastTargetSwitchTime;
    public EntityId LastKnownTarget; // Track to detect target changes
    
    // --- Stuck Prevention ---
    public float StuckCheckTimer;
    public Vector3 LastStuckCheckPosition;
    public int ConsecutiveStuckChecks; // How many times we've been stuck in a row

    // --- Helpers ---
    public bool CanJumpAttack => Time.time >= LastJumpTime + JumpAttackCooldown;
    public bool CanFlamethrower => Time.time >= LastFlamethrowerTime + FlamethrowerCooldown;
    public bool CanHammerSlam => Time.time >= LastHammerSlamTime + HammerSlamCooldown;
    public bool CanSwitchTarget => Time.time >= LastTargetSwitchTime + TargetSwitchCooldown;
}

public enum BossPhase
{
    Normal,
    Enraged,
}
