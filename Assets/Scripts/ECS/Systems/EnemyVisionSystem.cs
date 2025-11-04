using UnityEngine;

public class EnemyVisionSystem : ISystem
{
    private World _world;
    private readonly Collider[] _queryBuffer = new Collider[64];

    public void Initialize(World world)
    {
        _world = world;
    }

    public void Update(float dt)
    {
        foreach (var (entity, enemy, trans) in _world.Components.Query<EnemyComponent, TransformComponent>())
        {
            enemy.TimeSinceLastCheck += dt;
            if (enemy.TimeSinceLastCheck < enemy.CheckInterval)
                continue;

            enemy.TimeSinceLastCheck = 0f;

            Vector3 origin = trans.Position;
            int hits = Physics.OverlapSphereNonAlloc(
                origin,
                enemy.DetectionRange,
                _queryBuffer,
                enemy.DetectionMask,
                QueryTriggerInteraction.Ignore
            );

            EntityId closest = default;
            float bestSqr = float.MaxValue;

            for (int i = 0; i < hits; i++)
            {
                Collider col = _queryBuffer[i];
                if (!col.TryGetComponent(out EntityView view))
                    continue;

                EntityId candidate = view.EntityInstance;
                if (!_world.Components.Has<PlayerTagComponent>(candidate))
                    continue;

                Vector3 candidatePos = view.transform.position;
                Vector3 dir = candidatePos - origin;
                Vector3 dirFlat = new Vector3(dir.x, 0f, dir.z);
                float dsq = dirFlat.sqrMagnitude;

                // Out of range
                if (dsq > enemy.DetectionRange * enemy.DetectionRange)
                    continue;

                // FOV check
                Vector3 forward = trans.Rotation * Vector3.forward;
                float angle = Vector3.Angle(forward, dirFlat.normalized);
                if (angle > enemy.FieldOfView * 0.5f)
                    continue;

                // LOS check
                Vector3 rayStart = origin + Vector3.up * 0.5f;
                Vector3 rayDir = candidatePos - rayStart;
                float rayDist = rayDir.magnitude;
                rayDir /= rayDist;

                if (
                    Physics.Raycast(
                        rayStart,
                        rayDir,
                        out RaycastHit hit,
                        rayDist,
                        GridSystem.Instance.GetObstacleLayer()
                    )
                )
                    continue;

                if (dsq < bestSqr)
                {
                    bestSqr = dsq;
                    closest = candidate;
                }
            }

            if (!closest.Equals(default))
            {
                // New player detected
                if (enemy.TargetEntity != closest)
                {
                    enemy.TargetEntity = closest;
                    ReactToPlayerDetection(_world, entity, closest);
                }
            }
            else
            {
                HandleLostTarget(_world, entity, enemy, trans);
            }
        }
    }

    private void ReactToPlayerDetection(World world, EntityId enemyEntity, EntityId playerEntity)
    {
        var weapon = world.Components.Get<WeaponDataComponent>(enemyEntity);
        var enemy = world.Components.Get<EnemyComponent>(enemyEntity);
        var enemyTf = world.Components.Get<TransformComponent>(enemyEntity);
        var playerTf = world.Components.Get<TransformComponent>(playerEntity);

        float distance = Vector3.Distance(enemyTf.Position, playerTf.Position);

        // Player extremely close -> take cover
        if (distance < weapon.BaseRange * 0.7f && Time.time - enemy.LastCoverTime > enemy.CoverCooldown)
        {
            EnemyAIHelpers.ChangeState(world, enemyEntity, EnemyState.TakeCover);
        }
        // Player within attack range -> attack immediately
        else if (distance <= weapon.BaseRange)
        {
            EnemyAIHelpers.ChangeState(world, enemyEntity, EnemyState.Attack);
        }
        // Player visible but far -> chase
        else
        {
            EnemyAIHelpers.ChangeState(world, enemyEntity, EnemyState.Chase);
        }
    }

    private void HandleLostTarget(World world, EntityId enemyEntity, EnemyComponent enemy, TransformComponent trans)
    {
        if (!enemy.TargetEntity.Equals(default))
        {
            if (world.Components.TryGet(enemy.TargetEntity, out TransformComponent playerTf))
            {
                float sqr = (playerTf.Position - trans.Position).sqrMagnitude;
                if (sqr > enemy.LoseTargetRange * enemy.LoseTargetRange)
                {
                    world.Events.Publish(new EnemyPlayerLostEvent(enemyEntity, enemy.TargetEntity));
                }
            }
            else
            {
                world.Events.Publish(new EnemyPlayerLostEvent(enemyEntity, enemy.TargetEntity));
            }
        }
    }

    public void FixedUpdate(float dt) { }

    public void Shutdown() { }
}
