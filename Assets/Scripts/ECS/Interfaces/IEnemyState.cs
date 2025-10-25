using UnityEngine;

public interface IEnemyState
{
    public void OnEnter(World world, EntityId entity);
    public void OnUpdate(World world, EntityId entity, float dt);
    public void OnExit(World world, EntityId entity);

    public EnemyState StateType { get; }
}
