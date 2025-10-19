using UnityEngine;

[CreateAssetMenu(fileName = "AIConfig", menuName = "AI/AIConfig")]
public class AIConfig : ScriptableObject
{
    [Header("Detection")]
    public float detectionRange = 10f;

    [Header("Attack")]
    public float attackRange = 2f;
    public float attackCooldown = 1f;

    [Header("Patrol")]
    public float patrolSpeedMultiplier = 0.8f;
    public float patrolToIdleTime = 10f;
    public float idleToPatrolTime = 5f;

    [Header("Take Cover")]
    public float coverRange = 5f;
    public LayerMask coverLayer;
    public float coverTime = 3f;
}
