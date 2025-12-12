using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "AI/Enemy Definition")]
public class EnemyDefinitionSO : ScriptableObject
{
    public GameObject prefab;

    [Header("Base Stats")]
    public string enemyName;
    public float maxHealth = 100f;
    public float moveSpeed = 3f;
    public float attackRange = 1.5f;
    public float attackCooldown = 1.5f;
    public float damage = 10f;
    public bool isRanged;

    [Header("Animation")]
    public string isMovingParam = "isMoving";
    public string isRunningParam = "isRunning";
    public string moveXParam = "moveX";
    public string moveYParam = "moveY";
    public int totalAttackAnimations = 1;
    public string attackAnimationTrigger = "attack";
    public string takeCoverParam = "takeCover";

    [Header("Vision")]
    public float detectionRange = 10f;
    public float loseTargetRange = 15f;
    public float fieldOfView = 120f;
    public float checkInterval = 0.5f;
    public LayerMask detectionMask;

    [Header("Patrol Settings")]
    public bool generatePatrolPoints = true;
    public int patrolPointCount = 3;
    public float patrolRadius = 4f;

    [Header("AI Behavior")]
    public EnemyState defaultState = EnemyState.Idle;

    [Header("Audio")]
    public AudioProfileSO audioProfile;

    [Header("Attacks")]
    public List<AttackDefinition> attacks = new();
}
