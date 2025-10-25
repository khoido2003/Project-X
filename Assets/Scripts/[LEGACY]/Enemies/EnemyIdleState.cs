using System;
using UnityEngine;

public class EnemyIdleStateOld : AIState, IAnimationTrigger
{
    private float enterTime;
    private AIStateMachine machine;

    public event Action<string> OnTriggerAnimation;
    public event Action<string, float> OnSetFloatParameter;
    public event Action<string, bool> OnSetBoolParameter;

    public override void Enter(AIStateMachine machine)
    {
        this.machine = machine;

        this.machine.Pathfinding.Stop();

        OnSetBoolParameter?.Invoke("isMoving", false);

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

        if (Time.time - enterTime >= machine.GetAIConfig().idleToPatrolTime)
        {
            machine.TransitionTo(AIStateName.PATROL);
        }
    }

    public override void Exit() { }
}
