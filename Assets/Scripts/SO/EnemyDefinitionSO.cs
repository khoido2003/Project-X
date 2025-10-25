using System.Collections.Generic;
using UnityEngine;

public enum EnemyState
{
    Idle,
    Patrol,
    Chase,
    Attack,
    Dead,
}

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
    public string moveXParam = "moveX";
    public string moveYParam = "moveY";

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

    [Header("Weapon Settings")]
    public bool hasWeapon = true;
    public WeaponData weaponData;

    [System.Serializable]
    public class WeaponData
    {
        [Header("Config")]
        public string weaponName;
        public AttackExecutionType ExecutionType;

        [Header("Stats")]
        public float attackDamage = 11f;
        public float attackCooldown = 1.5f;
        public float attackRange = 2f;

        [Header("Visuals")]
        public ParticleSystem hitImpactParticlePrefab;

        [Header("Animation & Audio")]
        public string attackAnimationTrigger = "attack";
        public int totalAttackAnimations = 3;
        public AudioClip attackSound;
    }
}
