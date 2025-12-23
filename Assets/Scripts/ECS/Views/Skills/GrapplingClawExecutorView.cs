using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class GrapplingClawExecutorView : SkillExecutorView
{
    public override SkillCategory Category => SkillCategory.GrapplingClaw;

    private LineRenderer _grappleLine;

    protected override void ExecuteSkill(SkillConfirmExecutionEvent @event)
    {
        if (!NetworkManager.Singleton.IsServer)
        {
            return;
        }

        if (@event.Skill is not GrapplingClawSkillSO skill)
        {
            return;
        }

        EntityViewRegistry registry = WorldInstance.Services.Resolve<EntityViewRegistry>();
        if (!registry.TryGet(@event.Caster, out EntityView view))
        {
            return;
        }

        // Find target enemy in direction
        EntityId target = FindGrappleTarget(view.transform.position, @event.Direction, skill.grappleRange);

        if (!target.Equals(default))
        {
            StartCoroutine(GrappleRoutine(view.gameObject, skill, target));
        }
        else
        {
            // No valid target - skill fails but goes on cooldown
            FinishSkill(skill);
        }

        base.ExecuteSkill(@event);
    }

    private EntityId FindGrappleTarget(Vector3 origin, Vector3 direction, float range)
    {
        // Raycast to find enemy in direction
        RaycastHit[] hits = Physics.RaycastAll(origin + Vector3.up * 0.5f, direction, range);

        float closestDist = float.MaxValue;
        EntityId closestTarget = default;

        foreach (var hit in hits)
        {
            if (!hit.collider.TryGetComponent(out EntityView targetView))
            {
                continue;
            }

            EntityId targetEntity = targetView.EntityInstance;

            // Skip self
            if (targetEntity.Equals(EntityInstance))
            {
                continue;
            }

            // Must be an enemy
            if (!WorldInstance.Components.Has<EnemyComponent>(targetEntity))
            {
                continue;
            }

            // Must be alive
            if (WorldInstance.Components.TryGet(targetEntity, out HealthDataComponent health) && health.IsDead)
            {
                continue;
            }

            if (hit.distance < closestDist)
            {
                closestDist = hit.distance;
                closestTarget = targetEntity;
            }
        }

        return closestTarget;
    }

    private IEnumerator GrappleRoutine(GameObject owner, GrapplingClawSkillSO skill, EntityId targetEntity)
    {
        EntityViewRegistry registry = WorldInstance.Services.Resolve<EntityViewRegistry>();
        if (!registry.TryGet(targetEntity, out EntityView targetView))
        {
            FinishSkill(skill);
            yield break;
        }

        CharacterController controller = owner.GetComponent<CharacterController>();
        if (controller == null)
        {
            FinishSkill(skill);
            yield break;
        }

        Vector3 startPos = owner.transform.position;
        Vector3 targetPos = targetView.transform.position;

        // Broadcast visual to clients
        GrappleVisualClientRpc(startPos, targetPos, skill.grappleSpeed);

        // Play grapple fire sound
        if (skill.grappleFireSound != null)
        {
            AudioHelper.PlaySound3D(WorldInstance, skill.grappleFireSound, AudioCategory.Player, startPos);
        }

        // Calculate travel time
        float distance = Vector3.Distance(startPos, targetPos);
        float travelTime = distance / skill.grappleSpeed;
        float elapsed = 0f;

        // Move player to target
        while (elapsed < travelTime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / travelTime);

            // Update target position in case target moves
            if (registry.TryGet(targetEntity, out EntityView updatedTarget))
            {
                targetPos = updatedTarget.transform.position;
            }

            Vector3 newPos = Vector3.Lerp(startPos, targetPos, t);
            newPos.y = startPos.y; // Keep on same Y level

            controller.Move(newPos - owner.transform.position);

            yield return null;
        }

        // On arrival - stun target and deal damage
        ApplyGrappleImpact(skill, targetEntity, targetPos);

        FinishSkill(skill);
    }

    private void ApplyGrappleImpact(GrapplingClawSkillSO skill, EntityId targetEntity, Vector3 impactPos)
    {
        // Deal impact damage
        WorldInstance.Events.Publish(new DamageEvent
        {
            Target = targetEntity,
            Attacker = EntityInstance,
            Amount = skill.impactDamage,
        });

        // Apply stun
        if (WorldInstance.Components.TryGet(targetEntity, out MovementDataComponent movement))
        {
            movement.IsStunned = true;
            // movement.StunEndTime = Time.time + skill.stunDuration;
        }

        // Play impact sound
        if (skill.impactSound != null)
        {
            AudioHelper.PlaySound3D(WorldInstance, skill.impactSound, AudioCategory.Player, impactPos);
        }

        // Spawn impact VFX
        if (skill.impactVfxPrefab != null)
        {
            var vfx = Instantiate(skill.impactVfxPrefab, impactPos, Quaternion.identity);
            vfx.Play();
            Destroy(vfx.gameObject, 2f);
        }
    }

    [ClientRpc]
    private void GrappleVisualClientRpc(Vector3 startPos, Vector3 endPos, float speed)
    {
        // Create a simple line renderer to show grapple
        StartCoroutine(ClientGrappleVisualRoutine(startPos, endPos, speed));
    }

    private IEnumerator ClientGrappleVisualRoutine(Vector3 startPos, Vector3 endPos, float speed)
    {
        // Create temporary line renderer
        GameObject lineObj = new GameObject("GrappleLine");
        LineRenderer line = lineObj.AddComponent<LineRenderer>();
        line.positionCount = 2;
        line.startWidth = 0.1f;
        line.endWidth = 0.05f;
        line.material = new Material(Shader.Find("Sprites/Default"));
        line.startColor = Color.yellow;
        line.endColor = Color.red;

        float distance = Vector3.Distance(startPos, endPos);
        float travelTime = distance / speed;
        float elapsed = 0f;

        while (elapsed < travelTime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / travelTime);

            Vector3 currentEnd = Vector3.Lerp(startPos, endPos, t);
            line.SetPosition(0, transform.position);
            line.SetPosition(1, currentEnd);

            yield return null;
        }

        Destroy(lineObj);
    }

    protected override void SpawnClientVisualEffect(SkillEffectTriggerEvent @event)
    {
        // Client visuals handled via RPC
    }

    protected override void OnDestroy()
    {
        StopAllCoroutines();
        base.OnDestroy();
    }
}
