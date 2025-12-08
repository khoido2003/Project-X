using UnityEngine;

public class EnemyAttackStateAI : IEnemyState
{
    public EnemyState StateType => EnemyState.Attack;

    private readonly float rotateSpeed = 120f;

    public void OnEnter(World world, EntityId entity)
    {
        var enemy = world.Components.Get<EnemyComponent>(entity);
        enemy.StateTime = 0f;

        if (!enemy.TargetEntity.Equals(default))
        {
            FaceTarget(world, entity, enemy);
        }
    }

    public void OnUpdate(World world, EntityId entity, float dt)
    {
        var enemy = world.Components.Get<EnemyComponent>(entity);
        enemy.StateTime += dt;

        var weapon = world.Components.Get<WeaponDataComponent>(entity);

        enemy.StateTime += dt;

        if (enemy.TargetEntity.Equals(default))
        {
            EnemyAIHelpers.ChangeState(world, entity, EnemyState.Patrol);
            return;
        }

        if (!world.Components.TryGet(enemy.TargetEntity, out TransformComponent targetTransform))
        {
            enemy.TargetEntity = default;
            EnemyAIHelpers.ChangeState(world, entity, EnemyState.Patrol);
            return;
        }

        TransformComponent enemyTransform = world.Components.Get<TransformComponent>(entity);

        float dist = Vector3.Distance(targetTransform.Position, enemyTransform.Position);

        if (dist > weapon.BaseRange * 1.1f)
        {
            EnemyAIHelpers.ChangeState(world, entity, EnemyState.Chase);
            return;
        }

        FaceTarget(world, entity, enemy);

        // Attack on cooldown
        if (world.Components.TryGet(entity, out AttackDataComponent attack))
        {
            if (attack.CanAttack(weapon.BaseCooldown) && !attack.IsAttacking)
            {
                PerformAttack(world, entity, targetTransform.Position);
            }
        }

        // Take cover when player gets too close
        if (Time.time - enemy.LastCoverTime > enemy.CoverCooldown)
        {
            if (dist < weapon.BaseRange * 0.5f)
            {
                EnemyAIHelpers.ChangeState(world, entity, EnemyState.TakeCover);
                return;
            }
        }
    }

    private void FaceTarget(World world, EntityId entity, EnemyComponent enemy)
    {
        if (!world.Components.TryGet(enemy.TargetEntity, out TransformComponent targetTf))
        {
            return;
        }

        TransformComponent enemyTf = world.Components.Get<TransformComponent>(entity);

        Vector3 dir = targetTf.Position - enemyTf.Position;
        dir.y = 0f;

        if (dir.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(dir.normalized);

            enemyTf.Rotation = Quaternion.RotateTowards(enemyTf.Rotation, targetRotation, rotateSpeed * Time.deltaTime);

            var registry = world.Services.Resolve<EntityViewRegistry>();
            if (registry.TryGet(entity, out EntityView view))
            {
                view.transform.rotation = enemyTf.Rotation;
            }
        }
    }

    private void PerformAttack(World world, EntityId entity, Vector3 targetPos)
    {
        var attack = world.Components.Get<AttackDataComponent>(entity);
        var enemyTf = world.Components.Get<TransformComponent>(entity);

        // Set attack direction
        Vector3 direction = (targetPos - enemyTf.Position).normalized;
        attack.AttackDirection = direction;
        attack.IsAttacking = true;
        attack.LastAttackTime = Time.time;

        // Trigger animation
        if (world.Components.TryGet(entity, out AnimationDataComponent animation))
        {
            // int randomIndex = Random.Range(0, animation.TotalAttackAnimations);
            //
            // world.Events.Publish(
            //     new AnimationParameterEvent(entity, "attackIndex", AnimationParameterType.Float, randomIndex)
            // );

            world.Events.Publish(
                new AnimationParameterEvent(entity, animation.AttackTrigger, AnimationParameterType.Trigger, null)
            );
        }
    }

    public void OnExit(World world, EntityId entity) { }
}
