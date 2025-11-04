using UnityEngine;
using UnityEngine.UIElements;

public struct CoverSettings
{
    public float ScanRadius;
    public float IdealDistance;
    public float SpotOffset;
    public float MaxTravelDistance;

    public float DistanceWeight;
    public float AngleWeight;
    public float TravelPenaltyWeight;
}

public class EnemyTakeCoverStateAI : IEnemyState
{
    public EnemyState StateType => EnemyState.TakeCover;

    private static readonly CoverSettings CoverConfig = new CoverSettings
    {
        ScanRadius = 15f,
        IdealDistance = 10f,
        SpotOffset = 2f,
        MaxTravelDistance = 15f,

        DistanceWeight = 0.5f,
        AngleWeight = 0.4f,
        TravelPenaltyWeight = 0.2f,
    };

    private const float COVER_DURATION = 2.5f;
    private const float REACHED_THRESHOLD = 0.5f;

    public void OnEnter(World world, EntityId entity)
    {
        var enemy = world.Components.Get<EnemyComponent>(entity);
        enemy.StateTime = 0f;

        Vector3? coverSpot = FindNeearesstCoverSpot(world, entity, CoverConfig);
        if (coverSpot.HasValue)
        {
            enemy.CoverSpot = coverSpot.Value;
            world.Events.Publish(new EnemyPathRequestEvent(entity, enemy.CoverSpot, enemy.StoppingDistance));
        }
        else
        {
            EnemyAIHelpers.ChangeState(world, entity, EnemyState.Attack);
        }

        AnimationDataComponent animation = world.Components.Get<AnimationDataComponent>(entity);

        world.Events.Publish(
            new AnimationParameterEvent(entity, animation.IsRunningParam, AnimationParameterType.Bool, true)
        );
    }

    public void OnExit(World world, EntityId entity)
    {
        if (world.Components.TryGet(entity, out AnimationDataComponent anim))
        {
            world.Events.Publish(
                new AnimationParameterEvent(entity, anim.IsRunningParam, AnimationParameterType.Bool, false)
            );
        }
    }

    public void OnUpdate(World world, EntityId entity, float dt)
    {
        var enemy = world.Components.Get<EnemyComponent>(entity);
        var enemyTf = world.Components.Get<TransformComponent>(entity);

        enemy.StateTime += dt;

        if (!enemy.IsReachCoverSpot && Vector3.Distance(enemyTf.Position, enemy.CoverSpot) <= REACHED_THRESHOLD)
        {
            enemy.IsReachCoverSpot = true;

            if (world.Components.TryGet(entity, out AnimationDataComponent anim))
            {
                world.Events.Publish(
                    new AnimationParameterEvent(entity, anim.IsRunningParam, AnimationParameterType.Bool, false)
                );
                world.Events.Publish(
                    new AnimationParameterEvent(entity, anim.TakeCoverParam, AnimationParameterType.Trigger, null)
                );
            }
        }

        if (enemy.StateTime >= COVER_DURATION)
        {
            enemy.LastCoverTime = Time.time;
            EnemyAIHelpers.ChangeState(world, entity, EnemyState.Attack);
            return;
        }

        if (enemy.TargetEntity.Equals(default))
        {
            EnemyAIHelpers.ChangeState(world, entity, EnemyState.Patrol);
            return;
        }
    }

    private Vector3? FindNeearesstCoverSpot(World world, EntityId entity, CoverSettings config)
    {
        EnemyComponent enemy = world.Components.Get<EnemyComponent>(entity);
        TransformComponent enemyTf = world.Components.Get<TransformComponent>(entity);
        TransformComponent playerTf = world.Components.Get<TransformComponent>(enemy.TargetEntity);

        if (playerTf == null)
        {
            return null;
        }

        LayerMask coverMask = LayerMask.GetMask("Cover");

        float scanRadius = 15f;
        Collider[] covers = Physics.OverlapSphere(enemyTf.Position, scanRadius, coverMask);

        if (covers.Length == 0)
        {
            EnemyAIHelpers.ChangeState(world, entity, EnemyState.Attack);
        }

        Vector3 bestSpot = Vector3.zero;
        Vector3 playerPos = playerTf.Position;
        float bestScore = float.MinValue;

        foreach (Collider cover in covers)
        {
            Vector3 basePos = cover.transform.position;
            Vector3 toPlayer = (playerPos - basePos).normalized;

            Vector3[] candicates =
            {
                basePos - toPlayer * config.SpotOffset,
                basePos + Vector3.Cross(Vector3.up, toPlayer) * config.SpotOffset,
                basePos - Vector3.Cross(Vector3.up, toPlayer) * config.SpotOffset,
            };

            foreach (Vector3 spot in candicates)
            {
                float score = EvaluateCoverSpot(spot, playerPos, enemyTf.Position, config);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestSpot = spot;
                }
            }
        }

        return bestScore > 0f ? bestSpot : null;
    }

    private float EvaluateCoverSpot(Vector3 spot, Vector3 playerPos, Vector3 enemyPos, CoverSettings config)
    {
        // Block line of sign from player
        if (!Physics.Linecast(spot + Vector3.up * 1.5f, playerPos + Vector3.up * 1.5f))
        {
            return -1f;
        }

        // Distance: prefer spots around the ideal distance
        float distToPlayer = Vector3.Distance(spot, playerPos);
        float distScore = 1f - Mathf.Abs(distToPlayer - config.IdealDistance) / config.IdealDistance;
        distScore = Mathf.Clamp01(distScore);

        // Travel Cost: prefer closer spot
        float distToEnemy = Vector3.Distance(spot, enemyPos);
        float travelPenalty = Mathf.Clamp01(distToEnemy / config.MaxTravelDistance);

        // Direction: prefer spot face away from the player
        Vector3 toPlayer = (playerPos - spot).normalized;
        Vector3 toEnemy = (enemyPos - spot).normalized;
        float angle = Vector3.Dot(toPlayer, toEnemy);
        // choose opposite angle
        float angleScore = Mathf.Clamp01(-angle);

        float score =
            (distScore * config.DistanceWeight)
            + (angleScore * config.AngleWeight)
            - (travelPenalty * config.TravelPenaltyWeight);

        return score;
    }
}
