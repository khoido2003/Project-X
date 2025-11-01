using UnityEngine;

public struct EnemyPlayerDetectedEvent
{
    public EntityId Enemy;
    public EntityId Player;

    public EnemyPlayerDetectedEvent(EntityId enemy, EntityId player)
    {
        Enemy = enemy;
        Player = player;
    }
}

public struct EnemyPlayerLostEvent
{
    public EntityId Enemy;
    public EntityId Player;

    public EnemyPlayerLostEvent(EntityId enemy, EntityId player)
    {
        Enemy = enemy;
        Player = player;
    }
}

public struct EnemyPathRequestEvent
{
    public EntityId Entity;
    public Vector3 Target;
    public float StoppingDistance;

    public EnemyPathRequestEvent(EntityId entity, Vector3 target, float stoppingDistance = 0f)
    {
        Entity = entity;
        Target = target;
        StoppingDistance = stoppingDistance;
    }
}

public struct EnemyPathCalculatedEvent
{
    public EntityId Entity;

    public EnemyPathCalculatedEvent(EntityId entity) => Entity = entity;
}
