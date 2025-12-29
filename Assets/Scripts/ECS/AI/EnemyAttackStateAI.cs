using UnityEngine;

public class EnemyAttackStateAI : IEnemyState
{
    public EnemyState StateType => EnemyState.Attack;

    // Increased rotation speed for faster aiming
    private readonly float rotateSpeed = 360f;

    public void OnEnter(World world, EntityId entity)
    {
        var enemy = world.Components.Get<EnemyComponent>(entity);
        var weapon = world.Components.Get<WeaponDataComponent>(entity);
        var attack = world.Components.Get<AttackDataComponent>(entity);

        attack.IsAttacking = false;
        enemy.StateTime = 0f;

        if (!enemy.TargetEntity.Equals(default))
        {
            // Immediately snap to face target when entering attack state
            SnapToFaceTarget(world, entity, enemy);

            // Fire immediately on entering the state if in range and off cooldown
            TryAttack(world, entity, enemy, weapon, attack);
        }
    }

    public void OnUpdate(World world, EntityId entity, float dt)
    {
        var enemy = world.Components.Get<EnemyComponent>(entity);
        var weapon = world.Components.Get<WeaponDataComponent>(entity);
        var attack = world.Components.Get<AttackDataComponent>(entity);

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

        // Drop target if player becomes untargetable (cloaked) or dead
        if (world.Components.TryGet(enemy.TargetEntity, out HealthDataComponent targetHealth))
        {
            if (targetHealth.IsUntargetable || targetHealth.IsDead)
            {
                enemy.TargetEntity = default;
                EnemyAIHelpers.ChangeState(world, entity, EnemyState.Patrol);
                return;
            }
        }

        TransformComponent enemyTransform = world.Components.Get<TransformComponent>(entity);

        float dist = Vector3.Distance(targetTransform.Position, enemyTransform.Position);

        if (dist > weapon.BaseRange * 1.1f)
        {
            // Boss: Check for special moves when target moved away
            if (enemy.IsBoss && world.Components.TryGet(entity, out BossComponent boss))
            {
                // Prefer jump attack to close distance
                if (boss.CanJumpAttack && dist >= boss.JumpAttackMinRange && dist <= boss.JumpAttackRange)
                {
                    EnemyAIHelpers.ChangeState(world, entity, EnemyState.JumpAttack);
                    return;
                }

                // Use flamethrower if jump is on cooldown and in range
                if (boss.CanFlamethrower && dist <= boss.FlamethrowerRange)
                {
                    EnemyAIHelpers.ChangeState(world, entity, EnemyState.Flamethrower);
                    return;
                }
            }

            EnemyAIHelpers.ChangeState(world, entity, EnemyState.Chase);
            return;
        }

        // Boss: Use flamethrower if in range and ready
        if (enemy.IsBoss && world.Components.TryGet(entity, out BossComponent bossComp))
        {
            if (bossComp.CanFlamethrower && dist <= bossComp.FlamethrowerRange)
            {
                EnemyAIHelpers.ChangeState(world, entity, EnemyState.Flamethrower);
                return;
            }
        }

        FaceTarget(world, entity, enemy);

        // Attack on cooldown ( reset if an animation end event was missed)
        if (attack.IsAttacking && Time.time - attack.LastAttackTime > weapon.BaseCooldown * 1.25f)
        {
            attack.IsAttacking = false;
        }

        TryAttack(world, entity, enemy, weapon, attack);

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

    private void TryAttack(
        World world,
        EntityId entity,
        EnemyComponent enemy,
        WeaponDataComponent weapon,
        AttackDataComponent attack
    )
    {
        if (enemy.TargetEntity.Equals(default))
        {
            return;
        }

        if (!world.Components.TryGet(enemy.TargetEntity, out TransformComponent targetTf))
        {
            return;
        }

        if (attack.CanAttack(weapon.BaseCooldown) && !attack.IsAttacking)
        {
            // Get target movement for prediction
            Vector3 targetVelocity = Vector3.zero;
            if (world.Components.TryGet(enemy.TargetEntity, out MovementDataComponent targetMovement))
            {
                targetVelocity = targetMovement.Velocity;
            }

            PerformAttack(world, entity, targetTf.Position, targetVelocity, weapon);
        }
    }

    /// <summary>
    /// Immediately snaps rotation to face target (used when entering attack state)
    /// </summary>
    private void SnapToFaceTarget(World world, EntityId entity, EnemyComponent enemy)
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
            enemyTf.Rotation = targetRotation; // Snap immediately

            var registry = world.Services.Resolve<EntityViewRegistry>();
            if (registry.TryGet(entity, out EntityView view))
            {
                view.transform.rotation = targetRotation;
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

    /// <summary>
    /// Perform attack with target prediction for better accuracy
    /// </summary>
    private void PerformAttack(
        World world,
        EntityId entity,
        Vector3 targetPos,
        Vector3 targetVelocity,
        WeaponDataComponent weapon
    )
    {
        var attack = world.Components.Get<AttackDataComponent>(entity);
        var enemyTf = world.Components.Get<TransformComponent>(entity);

        Vector3 toTarget = targetPos - enemyTf.Position;
        float distance = toTarget.magnitude;

        // Calculate predicted target position (lead the target)
        Vector3 predictedTargetPos = targetPos;

        // Only predict for ranged (projectile) attacks
        if (weapon.ProjectileSpeed > 0 && distance > 0)
        {
            // Time for projectile to reach target at current position
            float timeToTarget = distance / weapon.ProjectileSpeed;

            // Predict where target will be when projectile arrives
            predictedTargetPos = targetPos + targetVelocity * timeToTarget * 0.8f; // 0.8 factor for slight underprediction

            // Keep prediction on same height plane
            predictedTargetPos.y = targetPos.y;
        }

        // Set attack direction to predicted position
        Vector3 direction = (predictedTargetPos - enemyTf.Position).normalized;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.0001f)
        {
            direction = enemyTf.Rotation * Vector3.forward;
        }

        attack.AttackDirection = direction.normalized;
        attack.IsAttacking = true;
        attack.LastAttackTime = Time.time;

        // Trigger animation with random attack index
        if (world.Components.TryGet(entity, out AnimationDataComponent animation))
        {
            // Random attack animation selection (like player characters)
            if (weapon.TotalAttackAnimations > 1)
            {
                int randomIndex = Random.Range(0, weapon.TotalAttackAnimations);
                world.Events.Publish(
                    new AnimationParameterEvent(entity, "attackIndex", AnimationParameterType.Float, randomIndex)
                );
            }

            world.Events.Publish(
                new AnimationParameterEvent(entity, animation.AttackTrigger, AnimationParameterType.Trigger, null)
            );
        }
    }

    public void OnExit(World world, EntityId entity) { }
}
