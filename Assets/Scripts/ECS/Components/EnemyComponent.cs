using System.Collections.Generic;
using UnityEngine;

public enum EnemyState
{
    Idle,
    Patrol,
    Chase,
    TakeCover,
    Attack,
   
    JumpAttack,   // Boss-only: leap attack to close distance
    Flamethrower, // Boss-only: cone AoE damage
   
    Dead,
}

public class EnemyComponent
{
    // --- Core State ---
    public EnemyState CurrentState;
    public float StateTime;
    public EntityId TargetEntity;
    public bool IsRanged;
    public bool IsBoss = false;
    public EntityId LastAttacker = default;
    public float LastDamageTime;

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

    // --- Take Cover ---
    public float LastCoverTime;
    public float CoverCooldown = 3f;
    public Vector3 CoverSpot;
    public bool IsReachCoverSpot;

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

    // Dead
    public float DeathTimer;
    public bool RagdollSpawned;

    // --- Helpers ---
    public bool HasPath => Path != null && Path.Count > 0;
}
