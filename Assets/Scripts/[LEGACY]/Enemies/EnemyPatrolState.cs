using System;
using UnityEngine;

public class EnemyPatrolState : AIState, IAnimationTrigger
{
    private AIStateMachine machine;

    private int currentPatrolIndex;
    private float enterTime;

    private int failedAttempts = 0;
    private const int maxFailedAttempts = 3;

    public event Action<string> OnTriggerAnimation;
    public event Action<string, float> OnSetFloatParameter;
    public event Action<string, bool> OnSetBoolParameter;

    public override void Enter(AIStateMachine machine)
    {
        this.machine = machine;

        if (currentPatrolIndex >= machine.GetPatrolPoints().Length)
        {
            currentPatrolIndex = 0;
        }

        if (machine.GetPatrolPoints().Length > 0)
        {
            machine.Pathfinding.SetTargetPosition(machine.GetPatrolPoints()[currentPatrolIndex].position);
        }

        machine.Pathfinding.SetMoveSpeed(machine.Data.stats.moveSpeed * machine.GetAIConfig().patrolSpeedMultiplier);

        OnSetBoolParameter?.Invoke("isMoving", true);

        machine.Pathfinding.OnTargetReached += PathfindingComponent_OnTargetReached;
        machine.Pathfinding.OnPathFailed += PathfindingComponent_OnPathFailed;

        enterTime = Time.time;
    }

    public override void Tick()
    {
        if (machine.ClosestPlayer != null)
        {
            float distanceToPlayer = Vector3.Distance(machine.transform.position, machine.ClosestPlayer.position);

            if (distanceToPlayer <= machine.GetAIConfig().detectionRange)
            {
                machine.TransitionTo(AIStateName.CHASE);
                return;
            }
        }

        if (Time.time - enterTime >= machine.GetAIConfig().patrolToIdleTime)
        {
            machine.TransitionTo(AIStateName.IDLE);
        }
    }

    private void PathfindingComponent_OnPathFailed()
    {
        failedAttempts++;

        if (failedAttempts >= maxFailedAttempts)
        {
            machine.TransitionTo(AIStateName.IDLE);
            failedAttempts = 0;
            return;
        }

        currentPatrolIndex = (currentPatrolIndex + 1) % machine.GetPatrolPoints().Length;
        machine.Pathfinding.SetTargetPosition(machine.GetPatrolPoints()[currentPatrolIndex].position);
    }

    private void PathfindingComponent_OnTargetReached()
    {
        currentPatrolIndex = (currentPatrolIndex + 1) % machine.GetPatrolPoints().Length;

        machine.Pathfinding.SetTargetPosition(machine.GetPatrolPoints()[currentPatrolIndex].position);
    }

    public override void Exit()
    {
        machine.Pathfinding.OnTargetReached -= PathfindingComponent_OnTargetReached;
        machine.Pathfinding.OnPathFailed -= PathfindingComponent_OnPathFailed;
    }
}
