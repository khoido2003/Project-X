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
    private float _originalSpeed = -1f;
    private AudioSource _spinAudio;
    private Coroutine _activeCoroutine;

    protected override void Start()
    {
        base.Start();
        
        // Subscribe to death event to cleanup skill effects when player dies
        if (WorldInstance != null)
        {
            WorldInstance.Events.Subscribe<EntityDeathEvent>(OnEntityDeath);
        }
    }

    private void OnEntityDeath(EntityDeathEvent @event)
    {
        // Only cleanup if our entity died
        if (@event.Entity != EntityInstance) return;
        
        // Stop the skill and cleanup all effects
        if (_activeCoroutine != null)
        {
            StopCoroutine(_activeCoroutine);
        }
        CleanupBladeStorm();
    }

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

        // Stop any existing coroutine before starting new one
        if (_activeCoroutine != null)
        {
            StopCoroutine(_activeCoroutine);
            CleanupBladeStorm();
        }
        
        _activeCoroutine = StartCoroutine(BladeStormRoutine(view.gameObject, skill));

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

        // Apply movement slow - CRITICAL: Must restore first if already slowed, then re-apply
        if (WorldInstance.Components.TryGet(EntityInstance, out MovementDataComponent movement))
        {
            // If we already have a stored original speed, restore it first before applying new slow
            // This handles the case where skill is interrupted or re-used
            if (_originalSpeed >= 0)
            {
                movement.MoveSpeed = _originalSpeed;
            }
            
            // Now store the current (restored or original) speed
            _originalSpeed = movement.MoveSpeed;
            
            // Apply the slow multiplier
            movement.MoveSpeed *= skill.moveSpeedMultiplier;
        }

        // Start spinning audio - store in class field
        if (skill.spinLoopSound != null && _spinAudio == null)
        {
            _spinAudio = owner.AddComponent<AudioSource>();
            _spinAudio.clip = skill.spinLoopSound;
            _spinAudio.loop = true;
            _spinAudio.volume = 0.5f;
            _spinAudio.Play();
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

        // Play end sound
        if (skill.spinEndSound != null)
        {
            AudioHelper.PlaySound3D(WorldInstance, skill.spinEndSound, AudioCategory.Player, owner.transform.position);
        }

        // Cleanup everything
        CleanupBladeStorm();

        // Broadcast spin end
        BladeStormVisualClientRpc(false, 0f);

        FinishSkill(skill);
    }

    /// <summary>
    /// Ensures all BladeStorm effects are cleaned up properly
    /// </summary>
    private void CleanupBladeStorm()
    {
        _isSpinning = false;
        _activeCoroutine = null;

        // Restore movement speed
        if (_originalSpeed >= 0 && WorldInstance != null && WorldInstance.Components.TryGet(EntityInstance, out MovementDataComponent moveData))
        {
            moveData.MoveSpeed = _originalSpeed;
            _originalSpeed = -1f;  // Reset
        }

        // Stop spin audio
        if (_spinAudio != null)
        {
            _spinAudio.Stop();
            Destroy(_spinAudio);
            _spinAudio = null;
        }

        // Cleanup VFX
        if (_activeSpinVfx != null)
        {
            _activeSpinVfx.Stop();
            Destroy(_activeSpinVfx.gameObject, 1f);
            _activeSpinVfx = null;
        }
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
        
        // Get skill for VFX/Audio reference
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

        // Client-side audio for spin loop
        AudioSource clientAudio = null;
        if (skill.spinLoopSound != null)
        {
            clientAudio = gameObject.AddComponent<AudioSource>();
            clientAudio.clip = skill.spinLoopSound;
            clientAudio.loop = true;
            clientAudio.volume = 0.5f;
            clientAudio.spatialBlend = 1f;  // 3D sound
            clientAudio.Play();
        }

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            
            // Rotate for visual effect
            transform.Rotate(Vector3.up, skill.spinRotationSpeed * Time.deltaTime);
            
            yield return null;
        }

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

        // Play end sound on client
        if (skill.spinEndSound != null)
        {
            AudioHelper.PlaySound3D(WorldInstance, skill.spinEndSound, AudioCategory.Player, transform.position);
        }
    }

    protected override void SpawnClientVisualEffect(SkillEffectTriggerEvent @event)
    {
        // Client visuals handled via RPC
    }

    protected override void OnDestroy()
    {
        if (WorldInstance != null)
        {
            WorldInstance.Events.Unsubscribe<EntityDeathEvent>(OnEntityDeath);
        }
        
        StopAllCoroutines();
        CleanupBladeStorm();
        _damagedThisTick.Clear();
        base.OnDestroy();
    }
}
