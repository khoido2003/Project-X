using UnityEngine;

public struct TakeDamageEvent
{
    public EntityId Attacker;
    public EntityId Target;
    public float Damage;
    public Vector3 HitPosition;
}
