using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum AIStateName
{
    IDLE,
    PATROL,
    CHASE,
    TAKE_COVER,
    ATTACK,
    DIE,
}

public class AIStateMachine : MonoBehaviour
{
    [SerializeField]
    private AIConfig config;

    [SerializeField]
    private LayerMask playerLayer;

    public CharacterData Data;

    private Dictionary<AIStateName, AIState> states = new();
    private AIState currentState;

    public PathfindingComponent Pathfinding { get; private set; }
    public AttackComponent Attack { get; private set; }
    public HealthComponent Health { get; private set; }
    public AnimationControllerComponent Animation { get; private set; }
    public EnemyPatrolPoints PatrolPoints { get; private set; }

    public EnemyIdleState IdleState { get; private set; }
    public EnemyPatrolState PatrolState { get; private set; }

    public Transform ClosestPlayer { get; set; }

    private Transform[] patrolPoints;

    private void Awake()
    {
        Pathfinding = GetComponent<PathfindingComponent>();
        Attack = GetComponent<AttackComponent>();
        Health = GetComponent<HealthComponent>();
        Animation = GetComponent<AnimationControllerComponent>();
        PatrolPoints = GetComponent<EnemyPatrolPoints>();

        IdleState = GetComponent<EnemyIdleState>();
        PatrolState = GetComponent<EnemyPatrolState>();
    }

    private void Start()
    {
        RegisterState();

        if (Health != null)
        {
            Health.OnDeath += HealthComponent_OnDeath;
        }

        List<IAnimationTrigger> animationTriggerSource = new() { IdleState, PatrolState };

        Animation?.Bind(animationTriggerSource);

        TransitionTo(AIStateName.IDLE);
    }

    private void Update()
    {
        UpdateClosestPlayer();

        if (currentState != null)
        {
            currentState.Tick();
        }
    }

    private void UpdateClosestPlayer()
    {
        Collider[] playerColliders = Physics.OverlapSphere(transform.position, config.detectionRange, playerLayer);

        Character[] players = playerColliders
            .Select(c => c.GetComponent<Character>())
            .Where(c => c != null && c.GetIsPlayerControlled())
            .ToArray();

        if (players.Length > 0)
        {
            ClosestPlayer = players
                .OrderBy(p => Vector3.Distance(transform.position, p.transform.position))
                .First()
                .transform;
        }
        else
        {
            ClosestPlayer = null;
        }
    }

    public void TransitionTo(AIStateName stateName)
    {
        if (states.TryGetValue(stateName, out AIState newState))
        {
            currentState?.Exit();
            currentState = newState;
            currentState.Enter(this);
        }
        else
        {
            Debug.LogError($"State {stateName} not found!");
        }
    }

    public AIConfig GetAIConfig()
    {
        return config;
    }

    private void RegisterState()
    {
        if (PatrolPoints == null)
        {
            Debug.LogError("No patrol points components found!");
        }

        patrolPoints = PatrolPoints.GeneratePatrolPoints();
        states.Clear();

        AIState[] aiStateList = GetComponents<AIState>();

        foreach (AIState state in aiStateList)
        {
            states[state.stateName] = state;
        }
    }

    private void HealthComponent_OnDeath(object sender, EventArgs e)
    {
        TransitionTo(AIStateName.DIE);
    }

    private void OnDestroy()
    {
        if (Health != null)
        {
            Health.OnDeath -= HealthComponent_OnDeath;
        }
    }

    public Transform[] GetPatrolPoints()
    {
        return patrolPoints;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;

        Gizmos.DrawWireSphere(transform.position, config.detectionRange);
    }
}
