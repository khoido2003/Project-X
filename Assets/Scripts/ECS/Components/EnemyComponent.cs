using System.Collections.Generic;
using UnityEngine;

public class EnemyComponent
{
    // --- Core State ---
    public EnemyState CurrentState;
    public float StateTime;
    public EntityId TargetEntity;

    // --- Stats ---
    public float MaxHealth;
    public float CurrentHealth;
    public float MoveSpeed;
    public float AttackRange;
    public float AttackCooldown;
    public float Damage;
    public bool IsRanged;

    // --- Vision / Detection ---
    public LayerMask DetectionMask;
    public float DetectionRange = 8f;
    public float LoseTargetRange = 10f;
    public float FieldOfView = 120f;
    public float CheckInterval = 0.5f;
    public float TimeSinceLastCheck;

    // --- Patrol ---
    public List<Vector3> PatrolWaypoints = new();
    public int PatrolIndex;
    public float PatrolWaitTime = 1f;
    public float PatrolDuration = 10f;

    // --- Pathfinding / Movement ---
    public List<Vector3> Path = new();
    public int WaypointIndex;
    public Vector3 LastRequestedTarget = Vector3.positiveInfinity;
    public float LastRequestTime;
    public float RequestCooldown = 0.5f;
    public float StoppingDistance = 0.5f;

    // --- Movement diagnostics ---
    public Vector3 LastAgentPosition;
    public float NoProgressTimer;
    public float StuckTimer;

    // --- Helpers ---
    public bool HasPath => Path != null && Path.Count > 0;
}
