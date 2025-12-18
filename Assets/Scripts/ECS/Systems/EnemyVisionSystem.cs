using Unity.Netcode;
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
        if (!NetworkManager.Singleton.IsServer)
        {
            return;
        }

        int enemyCount = 0;
        foreach (var (entity, enemy, trans) in _world.Components.Query<EnemyComponent, TransformComponent>())
        {
            enemyCount++;

            if (enemy.CurrentState == EnemyState.Dead)
            {
                continue;
            }

            enemy.TimeSinceLastCheck += dt;
            if (enemy.TimeSinceLastCheck < enemy.CheckInterval)
            {
                continue;
            }

            enemy.TimeSinceLastCheck = 0f;

            // Use Unity transform position instead of ECS TransformComponent
            // ECS component may not reflect actual game object position
            var registry = _world.Services.Resolve<EntityViewRegistry>();
            Vector3 origin = trans.Position;
            if (registry.TryGet(entity, out EntityView view))
            {
                origin = view.transform.position;
            }

            int hits = Physics.OverlapSphereNonAlloc(
                origin,
                enemy.DetectionRange,
                _queryBuffer,
                enemy.DetectionMask,
                QueryTriggerInteraction.Ignore
            );

            if (hits == 0)
            {
                Debug.Log(
                    $"[EnemyVisionSystem] Entity {entity.Id}: No hits at {origin}, range: {enemy.DetectionRange}, mask: {enemy.DetectionMask.value}"
                );
            }
            else
            {
                Debug.Log($"[EnemyVisionSystem] Entity {entity.Id}: Found {hits} potential targets at {origin}");
            }

            EntityId closest = default;
            float bestSqr = float.MaxValue;

            for (int i = 0; i < hits; i++)
            {
                Collider col = _queryBuffer[i];
                if (!col.TryGetComponent(out EntityView foundView))
                {
                    continue;
                }

                EntityId candidate = foundView.EntityInstance;
                if (!_world.Components.Has<PlayerTagComponent>(candidate))
                {
                    continue;
                }

                Vector3 candidatePos = foundView.transform.position;
                Vector3 dir = candidatePos - origin;
                Vector3 dirFlat = new Vector3(dir.x, 0f, dir.z);
                float dsq = dirFlat.sqrMagnitude;

                // Out of range
                if (dsq > enemy.DetectionRange * enemy.DetectionRange)
                {
                    continue;
                }

                // FOV check
                Vector3 forward = trans.Rotation * Vector3.forward;
                float angle = Vector3.Angle(forward, dirFlat.normalized);
                if (angle > enemy.FieldOfView * 0.5f)
                {
                    continue;
                }

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
                {
                    continue;
                }

                if (dsq < bestSqr)
                {
                    bestSqr = dsq;
                    closest = candidate;
                }
            }

            if (!closest.Equals(default))
            {
                bool wasNewDetection = enemy.TargetEntity.Equals(default) || !enemy.TargetEntity.Equals(closest);

                enemy.TargetEntity = closest;

                // New player detected
                if (wasNewDetection)
                {
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

        if (!world.Components.TryGet(playerEntity, out TransformComponent playerTf))
        {
            return;
        }

        float distance = Vector3.Distance(enemyTf.Position, playerTf.Position);

        // Player extremely close -> take cover
        if (distance < weapon.BaseRange * 0.6f && Time.time - enemy.LastCoverTime > enemy.CoverCooldown)
        {
            EnemyAIHelpers.ChangeState(world, enemyEntity, EnemyState.TakeCover);
        }
        // Player within attack range -> attack immediately
        else if (distance <= weapon.BaseRange * 0.8f)
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
                    enemy.TargetEntity = default;

                    world.Events.Publish(new EnemyPlayerLostEvent(enemyEntity, enemy.TargetEntity));
                }
            }
            else
            {
                enemy.TargetEntity = default;
                world.Events.Publish(new EnemyPlayerLostEvent(enemyEntity, enemy.TargetEntity));
            }
        }
    }

    public void FixedUpdate(float dt) { }

    public void Shutdown() { }
}
