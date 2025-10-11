using UnityEngine;

public struct DamageEvent
{
    public EntityId Target;
    public EntityId Attacker;
    public float Amount;
    public Vector3 Hitpoint;

    public DamageEvent(EntityId target, EntityId attacker, float amount, Vector3 hitpoint)
    {
        Target = target;
        Attacker = attacker;
        Amount = amount;
        Hitpoint = hitpoint;
    }
}
