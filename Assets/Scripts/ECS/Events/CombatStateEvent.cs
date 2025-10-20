using UnityEngine;

public struct EnterCombatStateEvent
{
    public EntityId Entity;
    public CombatState TargetState;
}

public struct ExitCombatStateEvent
{
    public EntityId Entity;
    public CombatState TargetState;
}

public struct CombatStateChangedEvent
{
    public EntityId Entity;
    public CombatState Previous;
    public CombatState Current;
}
