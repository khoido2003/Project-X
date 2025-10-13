using UnityEngine;

public struct DamageEvent
{
    public EntityId Target;
    public EntityId Attacker;
    public float Amount;

    public DamageEvent(EntityId target, EntityId attacker, float amount)
    {
        Target = target;
        Attacker = attacker;
        Amount = amount;
    }
}
