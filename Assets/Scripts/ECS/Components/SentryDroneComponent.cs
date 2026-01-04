using UnityEngine;

/// <summary>
/// Component for Vex's Explosive Drone entity.
/// The drone flies toward the nearest enemy and explodes on contact or timeout.
/// </summary>
public class SentryDroneComponent
{
    /// <summary>
    /// The entity ID of the player who spawned this drone
    /// </summary>
    public EntityId Owner;

    /// <summary>
    /// Target position the drone is flying toward
    /// </summary>
    public Vector3 TargetPosition;

    /// <summary>
    /// Target entity (for tracking moving targets)
    /// </summary>
    public EntityId TargetEntity;

    /// <summary>
    /// Speed at which drone flies
    /// </summary>
    public float FlightSpeed;

    /// <summary>
    /// Time when the drone will auto-detonate
    /// </summary>
    public float DetonationTime;

    /// <summary>
    /// Radius of explosion damage
    /// </summary>
    public float ExplosionRadius;

    /// <summary>
    /// Damage dealt by explosion
    /// </summary>
    public float ExplosionDamage;

    /// <summary>
    /// Whether the drone has already exploded
    /// </summary>
    public bool HasExploded;

    /// <summary>
    /// Reference to the skill definition for VFX/SFX
    /// </summary>
    public SentryDroneSkillSO SkillData;

    /// <summary>
    /// Direct reference to the drone's EntityView.
    /// The drone is NOT registered in the global EntityViewRegistry to prevent
    /// enemy AI systems from targeting it or pathing to it.
    /// </summary>
    public EntityView DroneView;
}
