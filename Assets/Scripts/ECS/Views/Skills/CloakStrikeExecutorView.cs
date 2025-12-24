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
        }
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

        // Broadcast cloak start to all clients
        CloakVisualClientRpc(true);

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
        
        // Broadcast uncloak to clients
        CloakVisualClientRpc(false);

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

    [ClientRpc]
    private void CloakVisualClientRpc(bool isCloaking)
    {
        // Ensure renderers are populated (may not be set on client)
        if (_renderers == null || _renderers.Length == 0)
        {
            _renderers = GetComponentsInChildren<Renderer>();
        }

        if (_cloakMaterial == null)
        {
            Debug.LogWarning("[CloakStrike] No cloak material assigned!");
            return;
        }

        Debug.Log($"[CloakStrike] CloakVisualClientRpc - isCloaking: {isCloaking}, renderers: {_renderers?.Length ?? 0}");

        foreach (var renderer in _renderers)
        {
            if (renderer == null) continue;

            if (isCloaking)
            {
                // Store original materials
                _originalMaterials[renderer] = renderer.materials;

                // Apply cloak material to all slots
                Material[] cloakMats = new Material[renderer.materials.Length];
                for (int i = 0; i < cloakMats.Length; i++)
                {
                    cloakMats[i] = _cloakMaterial;
                }
                renderer.materials = cloakMats;
            }
            else
            {
                // Restore original materials
                if (_originalMaterials.TryGetValue(renderer, out Material[] originalMats))
                {
                    renderer.materials = originalMats;
                    Debug.Log($"[CloakStrike] Restored materials for {renderer.name}");
                }
                else
                {
                    Debug.LogWarning($"[CloakStrike] No original materials found for {renderer.name}");
                }
            }
        }

        if (!isCloaking)
        {
            _originalMaterials.Clear();
        }
    }

    protected override void SpawnClientVisualEffect(SkillEffectTriggerEvent @event)
    {
        if (@event.Skill is not CloakStrikeSkillSO skill) return;

        // Spawn cloak VFX on client
        if (skill.cloakVfxPrefab != null)
        {
            var vfx = Instantiate(skill.cloakVfxPrefab, transform.position, Quaternion.identity, transform);
            vfx.Play();
            Destroy(vfx.gameObject, skill.cloakDuration + 1f);
        }
    }

    protected override void OnDestroy()
    {
        if (WorldInstance != null)
        {
            WorldInstance.Events.Unsubscribe<AttackPerformedEvent>(OnAttackPerformed);
        }
        
        // Restore materials if destroyed while cloaked
        foreach (var kvp in _originalMaterials)
        {
            if (kvp.Key != null)
            {
                kvp.Key.materials = kvp.Value;
            }
        }
        _originalMaterials.Clear();
        
        StopAllCoroutines();
        base.OnDestroy();
    }
}
