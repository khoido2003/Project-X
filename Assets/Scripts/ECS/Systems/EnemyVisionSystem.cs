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
            {
                continue;
            }

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
                {
                    continue;
                }

                EntityId candicate = view.EntityInstance;

                if (!_world.Components.Has<PlayerTagComponent>(candicate))
                {
                    continue;
                }

                // Check distance
                Vector3 candicatePos = view.transform.position;
                Vector3 dir = candicatePos - origin;
                Vector3 dirFlat = new Vector3(dir.x, 0f, dir.z);

                float dsq = dirFlat.sqrMagnitude;

                if (dsq > enemy.DetectionRange * enemy.DetectionRange)
                {
                    continue;
                }

                // FOV check
                Vector3 forward = trans.Rotation * Vector3.forward;
                float angle = Vector3.Angle(forward, dirFlat.normalized);
                if (angle > enemy.FieldOfView / 2)
                {
                    continue;
                }

                // LOS check
                Vector3 rayStart = origin + Vector3.up * 0.5f;
                Vector3 rayDir = candicatePos - rayStart;
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
                    closest = candicate;
                }
            }

            if (!closest.Equals(default))
            {
                if (enemy.TargetEntity != closest)
                {
                    _world.Events.Publish(new EnemyPlayerDetectedEvent(entity, closest));
                }
            }
            else
            {
                if (!enemy.TargetEntity.Equals(default))
                {
                    if (_world.Components.TryGet(enemy.TargetEntity, out TransformComponent playerrTf))
                    {
                        float sqr = (playerrTf.Position - trans.Position).sqrMagnitude;

                        if (sqr > enemy.LoseTargetRange * enemy.LoseTargetRange)
                        {
                            _world.Events.Publish(new EnemyPlayerLostEvent(entity, enemy.TargetEntity));
                        }
                    }
                    else
                    {
                        _world.Events.Publish(new EnemyPlayerLostEvent(entity, enemy.TargetEntity));
                    }
                }
            }
        }
    }

    public void FixedUpdate(float dt) { }

    public void Shutdown() { }
}
