using UnityEngine;

public enum CombatState
{
    Idle,
    Attacking,
    CastingSkill,
}

public class CombatStateComponent
{
    public CombatState CurrentState;
    public float LastActionTime;
}
