using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Executor view for Vex's Overcharge Field skill (E).
/// Creates an area that buffs allies and debuffs enemies.
/// </summary>
public class OverchargeFieldExecutorView : SkillExecutorView
{
    public override SkillCategory Category => SkillCategory.OverchargeField;

    private bool _isFieldActive = false;
    private ParticleSystem _activeFieldVfx;
    private AudioSource _loopAudio;
    private Coroutine _activeCoroutine;
    private Vector3 _fieldCenter;
    private float _fieldRadius;
    
    // Track entities we've applied buffs/debuffs to
    private HashSet<EntityId> _buffedAllies = new();
    private HashSet<EntityId> _debuffedEnemies = new();
    private Dictionary<EntityId, float> _originalSpeeds = new();

    // Client-side visual tracking
    private ParticleSystem _clientVfx;
    private AudioSource _clientAudio;

    protected override void Start()
    {
        base.Start();

        if (WorldInstance != null)
        {
            WorldInstance.Events.Subscribe<EntityDeathEvent>(OnEntityDeath);
        }
    }

    private void OnEntityDeath(EntityDeathEvent @event)
    {
        if (@event.Entity != EntityInstance) return;

        if (_activeCoroutine != null)
        {
            StopCoroutine(_activeCoroutine);
        }
        CleanupField();
    }

    protected override void ExecuteSkill(SkillConfirmExecutionEvent @event)
    {
        if (!NetworkManager.Singleton.IsServer)
        {
            return;
        }

        if (@event.Skill is not OverchargeFieldSkillSO skill)
        {
            return;
        }

        EntityViewRegistry registry = WorldInstance.Services.Resolve<EntityViewRegistry>();
        if (!registry.TryGet(@event.Caster, out EntityView casterView))
        {
            return;
        }

        // Stop any existing field
        if (_activeCoroutine != null)
        {
            StopCoroutine(_activeCoroutine);
            CleanupField();
        }

        _fieldCenter = casterView.transform.position;
        _fieldRadius = skill.fieldRadius;

        _activeCoroutine = StartCoroutine(OverchargeFieldRoutine(skill));

        base.ExecuteSkill(@event);
    }

    private IEnumerator OverchargeFieldRoutine(OverchargeFieldSkillSO skill)
    {
        _isFieldActive = true;

        // Spawn field VFX on server
        if (skill.fieldVfxPrefab != null)
        {
            _activeFieldVfx = Instantiate(skill.fieldVfxPrefab, _fieldCenter, Quaternion.identity);
            _activeFieldVfx.transform.localScale = Vector3.one * (skill.fieldRadius / 2f); // Scale to radius
            _activeFieldVfx.Play();
        }

        // Play activate sound
        if (skill.activateSound != null)
        {
            AudioHelper.PlaySound3D(WorldInstance, skill.activateSound, AudioCategory.Player, _fieldCenter);
        }

        // Start loop audio
        if (skill.activeLoopSound != null)
        {
            var audioObj = new GameObject("OverchargeFieldAudio");
            audioObj.transform.position = _fieldCenter;
            _loopAudio = audioObj.AddComponent<AudioSource>();
            _loopAudio.clip = skill.activeLoopSound;
            _loopAudio.loop = true;
            _loopAudio.volume = 0.5f;
            _loopAudio.spatialBlend = 1f;
            _loopAudio.Play();
        }

        float elapsed = 0f;
        float tickInterval = 0.25f; // Check every 0.25s
        float nextTickTime = 0f;

        while (elapsed < skill.fieldDuration)
        {
            elapsed += Time.deltaTime;

            if (elapsed >= nextTickTime)
            {
                ApplyFieldEffects(skill);
                nextTickTime += tickInterval;
            }

            yield return null;
        }

        // Play deactivate sound
        if (skill.deactivateSound != null)
        {
            AudioHelper.PlaySound3D(WorldInstance, skill.deactivateSound, AudioCategory.Player, _fieldCenter);
        }

        CleanupField();
        FinishSkill(skill);
    }

    private void ApplyFieldEffects(OverchargeFieldSkillSO skill)
    {
        // Find all entities in radius
        Collider[] hits = Physics.OverlapSphere(_fieldCenter, _fieldRadius);
        HashSet<EntityId> currentAlliesInField = new();
        HashSet<EntityId> currentEnemiesInField = new();

        foreach (var hit in hits)
        {
            if (!hit.TryGetComponent(out EntityView targetView))
            {
                continue;
            }

            EntityId targetEntity = targetView.EntityInstance;

            // Check if alive
            if (WorldInstance.Components.TryGet(targetEntity, out HealthDataComponent health) && health.IsDead)
            {
                continue;
            }

            bool isPlayer = WorldInstance.Components.Has<PlayerTagComponent>(targetEntity);
            bool isEnemy = WorldInstance.Components.Has<EnemyComponent>(targetEntity);

            if (isPlayer)
            {
                currentAlliesInField.Add(targetEntity);

                // Apply buffs if not already buffed
                if (!_buffedAllies.Contains(targetEntity))
                {
                    ApplyAllyBuff(targetEntity, skill);
                    _buffedAllies.Add(targetEntity);
                }
            }
            else if (isEnemy)
            {
                currentEnemiesInField.Add(targetEntity);

                // Apply slow if not already slowed
                if (!_debuffedEnemies.Contains(targetEntity))
                {
                    ApplyEnemySlow(targetEntity, skill);
                    _debuffedEnemies.Add(targetEntity);
                }
            }
        }

        // Remove buffs from allies who left the field
        List<EntityId> alliesToRemove = new();
        foreach (var ally in _buffedAllies)
        {
            if (!currentAlliesInField.Contains(ally))
            {
                RemoveAllyBuff(ally);
                alliesToRemove.Add(ally);
            }
        }
        foreach (var ally in alliesToRemove)
        {
            _buffedAllies.Remove(ally);
        }

        // Remove slow from enemies who left the field
        List<EntityId> enemiesToRemove = new();
        foreach (var enemy in _debuffedEnemies)
        {
            if (!currentEnemiesInField.Contains(enemy))
            {
                RemoveEnemySlow(enemy);
                enemiesToRemove.Add(enemy);
            }
        }
        foreach (var enemy in enemiesToRemove)
        {
            _debuffedEnemies.Remove(enemy);
        }
    }

    private void ApplyAllyBuff(EntityId entity, OverchargeFieldSkillSO skill)
    {
        // Apply attack speed and damage buff via event
        WorldInstance.Events.Publish(new ApplyBuffEvent
        {
            Target = entity,
            BuffType = BuffType.AttackSpeedBoost,
            Value = skill.attackSpeedBoost,
            Duration = skill.fieldDuration + 1f // Slightly longer to ensure coverage
        });

        WorldInstance.Events.Publish(new ApplyBuffEvent
        {
            Target = entity,
            BuffType = BuffType.DamageBoost,
            Value = skill.damageBoost,
            Duration = skill.fieldDuration + 1f
        });

        Debug.Log($"[OverchargeField] Applied buffs to ally {entity.Id}");
    }

    private void RemoveAllyBuff(EntityId entity)
    {
        // Buffs are duration-based, they'll expire naturally
        Debug.Log($"[OverchargeField] Ally {entity.Id} left field");
    }

    private void ApplyEnemySlow(EntityId entity, OverchargeFieldSkillSO skill)
    {
        if (!WorldInstance.Components.TryGet(entity, out MovementDataComponent movement))
        {
            return;
        }

        // Store original speed
        if (!_originalSpeeds.ContainsKey(entity))
        {
            _originalSpeeds[entity] = movement.MoveSpeed;
        }

        // Apply slow
        movement.MoveSpeed = _originalSpeeds[entity] * (1f - skill.enemySlowPercent);

        Debug.Log($"[OverchargeField] Slowed enemy {entity.Id} by {skill.enemySlowPercent * 100}%");
    }

    private void RemoveEnemySlow(EntityId entity)
    {
        if (!WorldInstance.Components.TryGet(entity, out MovementDataComponent movement))
        {
            return;
        }

        // Restore original speed
        if (_originalSpeeds.TryGetValue(entity, out float originalSpeed))
        {
            movement.MoveSpeed = originalSpeed;
            _originalSpeeds.Remove(entity);
        }

        Debug.Log($"[OverchargeField] Restored speed for enemy {entity.Id}");
    }

    private void CleanupField()
    {
        _isFieldActive = false;
        _activeCoroutine = null;

        // Remove all remaining buffs/debuffs
        foreach (var ally in _buffedAllies)
        {
            RemoveAllyBuff(ally);
        }
        _buffedAllies.Clear();

        foreach (var enemy in _debuffedEnemies)
        {
            RemoveEnemySlow(enemy);
        }
        _debuffedEnemies.Clear();
        _originalSpeeds.Clear();

        // Cleanup VFX
        if (_activeFieldVfx != null)
        {
            _activeFieldVfx.Stop();
            Destroy(_activeFieldVfx.gameObject, 1f);
            _activeFieldVfx = null;
        }

        // Cleanup audio
        if (_loopAudio != null)
        {
            _loopAudio.Stop();
            Destroy(_loopAudio.gameObject);
            _loopAudio = null;
        }

        // Cleanup client visuals
        if (_clientVfx != null)
        {
            _clientVfx.Stop();
            Destroy(_clientVfx.gameObject);
            _clientVfx = null;
        }

        if (_clientAudio != null)
        {
            _clientAudio.Stop();
            Destroy(_clientAudio.gameObject);
            _clientAudio = null;
        }
    }

    protected override void SpawnClientVisualEffect(SkillEffectTriggerEvent @event)
    {
        if (@event.Skill is not OverchargeFieldSkillSO skill) return;

        StartCoroutine(ClientFieldVisualRoutine(skill));
    }

    private IEnumerator ClientFieldVisualRoutine(OverchargeFieldSkillSO skill)
    {
        var registry = WorldInstance.Services.Resolve<EntityViewRegistry>();
        if (!registry.TryGet(EntityInstance, out EntityView casterView))
        {
            yield break;
        }

        // Cleanup existing client visuals first
        CleanupField();

        Vector3 fieldCenter = casterView.transform.position;

        // Spawn field VFX on client
        if (skill.fieldVfxPrefab != null)
        {
            _clientVfx = Instantiate(skill.fieldVfxPrefab, fieldCenter, Quaternion.identity);
            _clientVfx.transform.localScale = Vector3.one * (skill.fieldRadius / 2f);
            _clientVfx.Play();
        }

        // Client audio
        if (skill.activeLoopSound != null)
        {
            var audioObj = new GameObject("OverchargeFieldAudio_Client");
            audioObj.transform.position = fieldCenter;
            _clientAudio = audioObj.AddComponent<AudioSource>();
            _clientAudio.clip = skill.activeLoopSound;
            _clientAudio.loop = true;
            _clientAudio.volume = 0.5f;
            _clientAudio.spatialBlend = 1f;
            _clientAudio.Play();
        }

        yield return new WaitForSeconds(skill.fieldDuration);

        // Cleanup will happen via CleanupField if entity dies/skill interrupted
        // Otherwise do normal cleanup here
        
        if (_clientVfx != null)
        {
            _clientVfx.Stop();
            Destroy(_clientVfx.gameObject, 1f);
            _clientVfx = null;
        }

        if (_clientAudio != null)
        {
            _clientAudio.Stop();
            Destroy(_clientAudio.gameObject);
            _clientAudio = null;
        }

        // Play deactivate sound on client
        if (skill.deactivateSound != null)
        {
            AudioHelper.PlaySound3D(WorldInstance, skill.deactivateSound, AudioCategory.Player, fieldCenter);
        }
    }

    protected override void OnDestroy()
    {
        if (WorldInstance != null)
        {
            WorldInstance.Events.Unsubscribe<EntityDeathEvent>(OnEntityDeath);
        }

        StopAllCoroutines();
        CleanupField();
        base.OnDestroy();
    }
}
