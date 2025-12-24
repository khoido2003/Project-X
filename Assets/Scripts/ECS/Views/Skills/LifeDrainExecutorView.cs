using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class LifeDrainExecutorView : SkillExecutorView
{
    public override SkillCategory Category => SkillCategory.LifeDrain;

    private ParticleSystem _activeDrainVfx;
    private HashSet<EntityId> _drainedThisTick = new();

    protected override void ExecuteSkill(SkillConfirmExecutionEvent @event)
    {
        if (!NetworkManager.Singleton.IsServer)
        {
            return;
        }

        if (@event.Skill is not LifeDrainSkillSO skill)
        {
            return;
        }

        EntityViewRegistry registry = WorldInstance.Services.Resolve<EntityViewRegistry>();
        if (!registry.TryGet(@event.Caster, out EntityView view))
        {
            return;
        }

        StartCoroutine(LifeDrainRoutine(view.gameObject, skill));

        base.ExecuteSkill(@event);
    }

    private IEnumerator LifeDrainRoutine(GameObject owner, LifeDrainSkillSO skill)
    {
        // Broadcast drain start to clients
        LifeDrainVisualClientRpc(true, skill.drainDuration);

        // Spawn drain VFX on server
        if (skill.drainVfxPrefab != null)
        {
            _activeDrainVfx = Instantiate(skill.drainVfxPrefab, owner.transform);
            _activeDrainVfx.transform.localPosition = Vector3.zero;
            _activeDrainVfx.Play();
        }

        // Start draining audio
        AudioSource drainAudio = null;
        if (skill.drainLoopSound != null)
        {
            drainAudio = owner.AddComponent<AudioSource>();
            drainAudio.clip = skill.drainLoopSound;
            drainAudio.loop = true;
            drainAudio.volume = 0.5f;
            drainAudio.Play();
        }

        float tickInterval = skill.drainDuration / skill.tickCount;
        float totalHealed = 0f;

        for (int i = 0; i < skill.tickCount; i++)
        {
            float healedThisTick = ApplyDrainDamage(owner.transform.position, skill);
            totalHealed += healedThisTick;
            _drainedThisTick.Clear();

            yield return new WaitForSeconds(tickInterval);
        }

        // Final heal summary log
        Debug.Log($"[LifeDrain] Total healed: {totalHealed}");

        // Cleanup
        if (drainAudio != null)
        {
            drainAudio.Stop();
            Destroy(drainAudio);
        }

        if (_activeDrainVfx != null)
        {
            _activeDrainVfx.Stop();
            Destroy(_activeDrainVfx.gameObject, 1f);
            _activeDrainVfx = null;
        }

        // Broadcast drain end
        LifeDrainVisualClientRpc(false, 0f);

        FinishSkill(skill);
    }

    private float ApplyDrainDamage(Vector3 center, LifeDrainSkillSO skill)
    {
        float damagePerTick = skill.damage / skill.tickCount;
        float totalHealed = 0f;

        // Find all enemies in radius
        Collider[] hits = Physics.OverlapSphere(center, skill.drainRadius);

        foreach (var hit in hits)
        {
            if (!hit.TryGetComponent(out EntityView targetView))
            {
                continue;
            }

            EntityId targetEntity = targetView.EntityInstance;

            // Skip self
            if (targetEntity.Equals(EntityInstance))
            {
                continue;
            }

            // Skip if already drained this tick
            if (_drainedThisTick.Contains(targetEntity))
            {
                continue;
            }

            // Must be an enemy
            if (!WorldInstance.Components.Has<EnemyComponent>(targetEntity))
            {
                continue;
            }

            // Must be alive
            if (WorldInstance.Components.TryGet(targetEntity, out HealthDataComponent targetHealth) && targetHealth.IsDead)
            {
                continue;
            }

            _drainedThisTick.Add(targetEntity);

            // Apply damage
            WorldInstance.Events.Publish(new DamageEvent
            {
                Target = targetEntity,
                Attacker = EntityInstance,
                Amount = damagePerTick,
            });

            // Calculate heal amount
            float healAmount = damagePerTick * skill.lifestealPercent;

            // Apply heal to caster
            if (WorldInstance.Components.TryGet(EntityInstance, out HealthDataComponent casterHealth))
            {
                casterHealth.CurrentHealth = Mathf.Min(casterHealth.CurrentHealth + healAmount, casterHealth.MaxHealth);
                totalHealed += healAmount;

                // Log heal for debugging
                Debug.Log($"[LifeDrain] Healed {healAmount} HP from {targetEntity.Id}");
            }
        }

        // Spawn heal VFX if healed any health
        if (totalHealed > 0 && skill.healVfxPrefab != null)
        {
            var healVfx = Instantiate(skill.healVfxPrefab, transform.position, Quaternion.identity, transform);
            healVfx.Play();
            Destroy(healVfx.gameObject, 2f);
        }

        return totalHealed;
    }

    [ClientRpc]
    private void LifeDrainVisualClientRpc(bool isStarting, float duration)
    {
        if (isStarting)
        {
            StartCoroutine(ClientDrainVisualRoutine(duration));
        }
    }

    private IEnumerator ClientDrainVisualRoutine(float duration)
    {
        // Get skill for VFX/Audio reference
        var skillBuffer = WorldInstance.Components.Get<SkillCastBufferComponent>(EntityInstance);
        if (skillBuffer.Skill is not LifeDrainSkillSO skill)
        {
            yield break;
        }

        ParticleSystem clientVfx = null;
        if (skill.drainVfxPrefab != null)
        {
            clientVfx = Instantiate(skill.drainVfxPrefab, transform);
            clientVfx.transform.localPosition = Vector3.zero;
            clientVfx.Play();
        }

        // Client-side audio for drain loop
        AudioSource clientAudio = null;
        if (skill.drainLoopSound != null)
        {
            clientAudio = gameObject.AddComponent<AudioSource>();
            clientAudio.clip = skill.drainLoopSound;
            clientAudio.loop = true;
            clientAudio.volume = 0.5f;
            clientAudio.spatialBlend = 1f;  // 3D sound
            clientAudio.Play();
        }

        yield return new WaitForSeconds(duration);

        // Cleanup VFX
        if (clientVfx != null)
        {
            clientVfx.Stop();
            Destroy(clientVfx.gameObject, 1f);
        }

        // Cleanup Audio
        if (clientAudio != null)
        {
            clientAudio.Stop();
            Destroy(clientAudio);
        }
    }

    protected override void SpawnClientVisualEffect(SkillEffectTriggerEvent @event)
    {
        // Client visuals handled via RPC
    }

    protected override void OnDestroy()
    {
        StopAllCoroutines();
        _drainedThisTick.Clear();
        base.OnDestroy();
    }
}
