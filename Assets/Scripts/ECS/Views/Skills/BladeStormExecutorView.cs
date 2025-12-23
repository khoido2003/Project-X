using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class BladeStormExecutorView : SkillExecutorView
{
    public override SkillCategory Category => SkillCategory.BladeStorm;

    private bool _isSpinning = false;
    private ParticleSystem _activeSpinVfx;
    private HashSet<EntityId> _damagedThisTick = new();

    protected override void ExecuteSkill(SkillConfirmExecutionEvent @event)
    {
        if (!NetworkManager.Singleton.IsServer)
        {
            return;
        }

        if (@event.Skill is not BladeStormSkillSO skill)
        {
            return;
        }

        EntityViewRegistry registry = WorldInstance.Services.Resolve<EntityViewRegistry>();
        if (!registry.TryGet(@event.Caster, out EntityView view))
        {
            return;
        }

        StartCoroutine(BladeStormRoutine(view.gameObject, skill));

        base.ExecuteSkill(@event);
    }

    private IEnumerator BladeStormRoutine(GameObject owner, BladeStormSkillSO skill)
    {
        _isSpinning = true;

        // Broadcast spin start to clients
        BladeStormVisualClientRpc(true, skill.spinDuration);

        // Spawn spin VFX on server
        if (skill.spinVfxPrefab != null)
        {
            _activeSpinVfx = Instantiate(skill.spinVfxPrefab, owner.transform);
            _activeSpinVfx.transform.localPosition = Vector3.zero;
            _activeSpinVfx.Play();
        }

        // Apply movement slow
        float originalSpeed = 1f;
        if (WorldInstance.Components.TryGet(EntityInstance, out MovementDataComponent movement))
        {
            originalSpeed = movement.MoveSpeed;
            movement.MoveSpeed *= skill.moveSpeedMultiplier;
        }

        // Start spinning audio
        AudioSource spinAudio = null;
        if (skill.spinLoopSound != null)
        {
            spinAudio = owner.AddComponent<AudioSource>();
            spinAudio.clip = skill.spinLoopSound;
            spinAudio.loop = true;
            spinAudio.volume = 0.5f;
            spinAudio.Play();
        }

        float elapsed = 0f;
        float nextTickTime = 0f;

        while (elapsed < skill.spinDuration)
        {
            elapsed += Time.deltaTime;

            // Rotate character for visual effect
            owner.transform.Rotate(Vector3.up, skill.spinRotationSpeed * Time.deltaTime);

            // Apply damage ticks
            if (elapsed >= nextTickTime)
            {
                ApplySpinDamage(owner.transform.position, skill);
                nextTickTime += skill.tickInterval;
                _damagedThisTick.Clear();
            }

            yield return null;
        }

        // Cleanup
        _isSpinning = false;

        // Restore movement speed
        if (WorldInstance.Components.TryGet(EntityInstance, out MovementDataComponent moveData))
        {
            moveData.MoveSpeed = originalSpeed;
        }

        // Stop spin audio
        if (spinAudio != null)
        {
            spinAudio.Stop();
            Destroy(spinAudio);
        }

        // Play end sound
        if (skill.spinEndSound != null)
        {
            AudioHelper.PlaySound3D(WorldInstance, skill.spinEndSound, AudioCategory.Player, owner.transform.position);
        }

        // Cleanup VFX
        if (_activeSpinVfx != null)
        {
            _activeSpinVfx.Stop();
            Destroy(_activeSpinVfx.gameObject, 1f);
            _activeSpinVfx = null;
        }

        // Broadcast spin end
        BladeStormVisualClientRpc(false, 0f);

        FinishSkill(skill);
    }

    private void ApplySpinDamage(Vector3 center, BladeStormSkillSO skill)
    {
        // Find all enemies in radius
        Collider[] hits = Physics.OverlapSphere(center, skill.spinRadius);
        
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

            // Skip if already damaged this tick
            if (_damagedThisTick.Contains(targetEntity))
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

            _damagedThisTick.Add(targetEntity);

            // Apply damage
            WorldInstance.Events.Publish(new DamageEvent
            {
                Target = targetEntity,
                Attacker = EntityInstance,
                Amount = skill.damagePerTick,
            });
        }
    }

    [ClientRpc]
    private void BladeStormVisualClientRpc(bool isStarting, float duration)
    {
        if (isStarting)
        {
            // Start spinning visual on client
            StartCoroutine(ClientSpinVisualRoutine(duration));
        }
    }

    private IEnumerator ClientSpinVisualRoutine(float duration)
    {
        float elapsed = 0f;
        
        // Get skill for VFX reference
        var skillBuffer = WorldInstance.Components.Get<SkillCastBufferComponent>(EntityInstance);
        if (skillBuffer.Skill is not BladeStormSkillSO skill)
        {
            yield break;
        }

        ParticleSystem clientVfx = null;
        if (skill.spinVfxPrefab != null)
        {
            clientVfx = Instantiate(skill.spinVfxPrefab, transform);
            clientVfx.transform.localPosition = Vector3.zero;
            clientVfx.Play();
        }

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            
            // Rotate for visual effect
            transform.Rotate(Vector3.up, skill.spinRotationSpeed * Time.deltaTime);
            
            yield return null;
        }

        if (clientVfx != null)
        {
            clientVfx.Stop();
            Destroy(clientVfx.gameObject, 1f);
        }
    }

    protected override void SpawnClientVisualEffect(SkillEffectTriggerEvent @event)
    {
        // Client visuals handled via RPC
    }

    protected override void OnDestroy()
    {
        StopAllCoroutines();
        _damagedThisTick.Clear();
        base.OnDestroy();
    }
}
