using UnityEngine;

public abstract class AIState : MonoBehaviour
{
    public AIStateName stateName;

    public abstract void Enter(AIStateMachine machine);
    public abstract void Tick();
    public abstract void Exit();
}
