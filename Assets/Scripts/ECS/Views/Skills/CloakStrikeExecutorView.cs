using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class CloakStrikeExecutorView : SkillExecutorView
{
    public override SkillCategory Category => SkillCategory.CloakStrike;

    [Header("Cloak Visual")]
    [SerializeField] private Material _cloakMaterial;

    private bool _isCloaked = false;
    private float _cloakEndTime;
    private float _bonusDamageMultiplier;
    private ParticleSystem _activeCloakVfx;
    private Renderer[] _renderers;
    private Dictionary<Renderer, Material[]> _originalMaterials = new();

    protected override void Start()
    {
        base.Start();
        _renderers = GetComponentsInChildren<Renderer>();

        // Subscribe to attack events to trigger empowered strike
        if (WorldInstance != null)
        {
            WorldInstance.Events.Subscribe<AttackPerformedEvent>(OnAttackPerformed);
            WorldInstance.Events.Subscribe<EntityDeathEvent>(OnEntityDeath);
        }
    }

    private void OnEntityDeath(EntityDeathEvent @event)
    {
        // Only cleanup if our entity died
        if (@event.Entity != EntityInstance) return;
        
        // Force cleanup cloak effects without triggering normal end logic
        StopAllCoroutines();
        ForceCleanupCloak();
    }

    /// <summary>
    /// Force cleanup all cloak effects - used when player dies during cloak
    /// </summary>
    private void ForceCleanupCloak()
    {
        _isCloaked = false;
        
        // Restore untargetable state
        if (WorldInstance != null && WorldInstance.Components.TryGet(EntityInstance, out HealthDataComponent health))
        {
            health.IsUntargetable = false;
        }
        
        // Cleanup VFX
        if (_activeCloakVfx != null)
        {
            _activeCloakVfx.Stop();
            Destroy(_activeCloakVfx.gameObject);
            _activeCloakVfx = null;
        }
        
        // Restore materials locally
        RestoreMaterials();
        
    }

    /// <summary>
    /// Stores original materials and applies cloak material to all renderers
    /// </summary>
    private void ApplyCloakMaterial()
    {
        // Ensure renderers are populated
        if (_renderers == null || _renderers.Length == 0)
        {
            _renderers = GetComponentsInChildren<Renderer>();
        }

        if (_cloakMaterial == null)
        {
            Debug.LogWarning("[CloakStrike] No cloak material assigned!");
            return;
        }

        foreach (var renderer in _renderers)
        {
            if (renderer == null) continue;

            // Only store if not already stored (prevents overwriting original with cloak material)
            if (!_originalMaterials.ContainsKey(renderer))
            {
                _originalMaterials[renderer] = renderer.materials;
            }

            // Apply cloak material to all slots
            Material[] cloakMats = new Material[renderer.materials.Length];
            for (int i = 0; i < cloakMats.Length; i++)
            {
                cloakMats[i] = _cloakMaterial;
            }
            renderer.materials = cloakMats;
        }
    }

    private void RestoreMaterials()
    {
        foreach (var kvp in _originalMaterials)
        {
            if (kvp.Key != null)
            {
                kvp.Key.materials = kvp.Value;
            }
        }
        _originalMaterials.Clear();
    }

    protected override void ExecuteSkill(SkillConfirmExecutionEvent @event)
    {
        if (!NetworkManager.Singleton.IsServer)
        {
            return;
        }

        if (@event.Skill is not CloakStrikeSkillSO skill)
        {
            return;
        }

        // Start cloaking
        StartCoroutine(CloakRoutine(skill));

        base.ExecuteSkill(@event);
    }

    private IEnumerator CloakRoutine(CloakStrikeSkillSO skill)
    {
        _isCloaked = true;
        _cloakEndTime = Time.time + skill.cloakDuration;
        _bonusDamageMultiplier = skill.bonusDamageMultiplier;

        // Make character untargetable
        if (WorldInstance.Components.TryGet(EntityInstance, out HealthDataComponent health))
        {
            health.IsUntargetable = true;
        }

        // Apply cloak material locally on server for host player visibility
        // Clients receive SkillEffectTriggerEvent and handle material change in SpawnClientVisualEffect
        ApplyCloakMaterial();

        // Spawn cloak VFX on server
        if (skill.cloakVfxPrefab != null)
        {
            _activeCloakVfx = Instantiate(skill.cloakVfxPrefab, transform.position, Quaternion.identity, transform);
            _activeCloakVfx.Play();
        }

        // Play cloak sound
        if (skill.cloakSound != null)
        {
            AudioHelper.PlaySound3D(WorldInstance, skill.cloakSound, AudioCategory.Player, transform.position);
        }

        // Wait for cloak duration or until attacking
        while (_isCloaked && Time.time < _cloakEndTime)
        {
            yield return null;
        }

        // End cloak
        EndCloak(skill);
    }

    private void EndCloak(CloakStrikeSkillSO skill)
    {
        if (!_isCloaked) return;
        
        _isCloaked = false;
        
        // Make character targetable again
        if (WorldInstance.Components.TryGet(EntityInstance, out HealthDataComponent health))
        {
            health.IsUntargetable = false;
        }
        
        // Restore materials locally on server
        RestoreMaterials();


        // Cleanup VFX
        if (_activeCloakVfx != null)
        {
            _activeCloakVfx.Stop();
            Destroy(_activeCloakVfx.gameObject, 1f);
            _activeCloakVfx = null;
        }

        // Spawn uncloak VFX
        if (skill.uncloakVfxPrefab != null)
        {
            var vfx = Instantiate(skill.uncloakVfxPrefab, transform.position, Quaternion.identity);
            vfx.Play();
            Destroy(vfx.gameObject, 2f);
        }

        FinishSkill(skill);
    }

    private void OnAttackPerformed(AttackPerformedEvent @event)
    {
        if (@event.Attacker != EntityInstance) return;
        
        // If cloaked, apply bonus damage and break cloak
        if (_isCloaked)
        {
            // Apply bonus damage via damage modifier event
            WorldInstance.Events.Publish(new DamageModifierEvent
            {
                Target = @event.Target,
                Attacker = EntityInstance,
                Multiplier = 1f + _bonusDamageMultiplier,
                Source = "CloakStrike"
            });
            
            // End cloak on attack
            var skillBuffer = WorldInstance.Components.Get<SkillCastBufferComponent>(EntityInstance);
            if (skillBuffer.Skill is CloakStrikeSkillSO skill)
            {
                EndCloak(skill);
            }
        }
    }


    protected override void SpawnClientVisualEffect(SkillEffectTriggerEvent @event)
    {
        if (@event.Skill is not CloakStrikeSkillSO skill) return;

        // Apply cloak material on client - [ClientRpc] doesn't work on MonoBehaviour, only NetworkBehaviour
        // So we handle material swap directly here when SkillEffectTriggerEvent is received
        StartCoroutine(ClientCloakRoutine(skill));
    }

    /// <summary>
    /// Client-side cloak routine that applies material change and spawns VFX
    /// </summary>
    private IEnumerator ClientCloakRoutine(CloakStrikeSkillSO skill)
    {
        // Ensure renderers are populated
        if (_renderers == null || _renderers.Length == 0)
        {
            _renderers = GetComponentsInChildren<Renderer>();
        }

        // Apply cloak material
        if (_cloakMaterial != null)
        {
            foreach (var renderer in _renderers)
            {
                if (renderer == null) continue;

                // Store original materials
                if (!_originalMaterials.ContainsKey(renderer))
                {
                    _originalMaterials[renderer] = renderer.materials;
                }

                // Apply cloak material to all slots
                Material[] cloakMats = new Material[renderer.materials.Length];
                for (int i = 0; i < cloakMats.Length; i++)
                {
                    cloakMats[i] = _cloakMaterial;
                }
                renderer.materials = cloakMats;
            }
        }

        // Spawn cloak VFX on client
        ParticleSystem clientVfx = null;
        if (skill.cloakVfxPrefab != null)
        {
            clientVfx = Instantiate(skill.cloakVfxPrefab, transform.position, Quaternion.identity, transform);
            clientVfx.Play();
        }

        // Wait for cloak duration
        yield return new WaitForSeconds(skill.cloakDuration);

        // Restore original materials
        RestoreMaterials();

        // Cleanup VFX
        if (clientVfx != null)
        {
            clientVfx.Stop();
            Destroy(clientVfx.gameObject, 1f);
        }

        // Spawn uncloak VFX on client
        if (skill.uncloakVfxPrefab != null)
        {
            var uncloakVfx = Instantiate(skill.uncloakVfxPrefab, transform.position, Quaternion.identity);
            uncloakVfx.Play();
            Destroy(uncloakVfx.gameObject, 2f);
        }
    }

    protected override void OnDestroy()
    {
        if (WorldInstance != null)
        {
            WorldInstance.Events.Unsubscribe<AttackPerformedEvent>(OnAttackPerformed);
            WorldInstance.Events.Unsubscribe<EntityDeathEvent>(OnEntityDeath);
        }
        
        // Restore materials if destroyed while cloaked
        RestoreMaterials();
        
        StopAllCoroutines();
        base.OnDestroy();
    }
}
