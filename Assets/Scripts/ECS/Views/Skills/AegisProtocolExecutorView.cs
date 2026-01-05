using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Executor view for Vex's AEGIS Protocol ultimate skill (R).
/// Transforms the character into a mech suit with enhanced stats.
/// </summary>
public class AegisProtocolExecutorView : SkillExecutorView
{
    public override SkillCategory Category => SkillCategory.AegisProtocol;

    [Header("Mech Visual Setup")]
    [SerializeField] private GameObject _characterModel;  // The normal character model to hide
    [SerializeField] private GameObject _mechModel;       // The mech model to show (child, disabled by default)
    
    [Header("Animator References")]
    [Tooltip("Animator for the character form - assign in inspector")]
    [SerializeField] private Animator _characterAnimatorRef;
    [Tooltip("Animator for the mech form - assign in inspector")]
    [SerializeField] private Animator _mechAnimatorRef;
    
    [Header("Health Bar Setup")]
    [SerializeField] private GameObject _characterHealthBar;  // Health bar for normal form
    [SerializeField] private GameObject _mechHealthBar;       // Health bar for mech form (disabled by default)

    private bool _isInMech = false;
    private Coroutine _activeCoroutine;
    private ParticleSystem _activeMechVfx;
    private AudioSource _mechLoopAudio;

    // Original stats storage
    private float _originalMoveSpeed;
    private float _originalMaxHealth;
    private float _storedBonusHealth;
    private float _originalAttackRange;

    // Cached AnimationView reference
    private AnimationView _animationView;

    protected override void Start()
    {
        base.Start();

        if (WorldInstance != null)
        {
            WorldInstance.Events.Subscribe<EntityDeathEvent>(OnEntityDeath);
            WorldInstance.Events.Subscribe<PlayerRespawnedEvent>(OnPlayerRespawned);
        }

        // Cache AnimationView reference
        _animationView = GetComponent<AnimationView>();
        
        // Log warning if animator references are not set
        if (_characterAnimatorRef == null)
        {
            Debug.LogWarning($"[AegisProtocol] Character Animator reference not set! Trying to find it dynamically.");
            if (_characterModel != null)
                _characterAnimatorRef = _characterModel.GetComponentInChildren<Animator>();
        }
        
        if (_mechAnimatorRef == null)
        {
            Debug.LogWarning($"[AegisProtocol] Mech Animator reference not set! Trying to find it dynamically.");
            if (_mechModel != null)
                _mechAnimatorRef = _mechModel.GetComponentInChildren<Animator>();
        }

        // Ensure mech model and health bar are hidden at start
        if (_mechModel != null)
        {
            _mechModel.SetActive(false);
        }
        
        if (_mechHealthBar != null)
        {
            _mechHealthBar.SetActive(false);
        }
        
        // CRITICAL: Explicitly set the character animator on ALL clients (including spectators)
        // This is necessary because AnimationView.Awake() may have cached the wrong animator
        // (e.g., the mech animator if it was higher in the hierarchy)
        if (_animationView != null && _characterAnimatorRef != null)
        {
            _characterAnimatorRef.Rebind();
            _characterAnimatorRef.Update(0);
            _animationView.SetAnimator(_characterAnimatorRef);
            Debug.Log("[AegisProtocol] Explicitly bound character animator on Start()");
        }
    }

    private void OnEntityDeath(EntityDeathEvent @event)
    {
        if (@event.Entity != EntityInstance) return;

        if (_activeCoroutine != null)
        {
            StopCoroutine(_activeCoroutine);
        }
        ForceExitMech();
    }

    protected override void ExecuteSkill(SkillConfirmExecutionEvent @event)
    {
        if (!NetworkManager.Singleton.IsServer)
        {
            return;
        }

        if (@event.Skill is not AegisProtocolSkillSO skill)
        {
            return;
        }

        // Can't enter mech if already in mech
        if (_isInMech)
            return;

        EntityViewRegistry registry = WorldInstance.Services.Resolve<EntityViewRegistry>();
        if (!registry.TryGet(@event.Caster, out EntityView casterView))
        {
            return;
        }

        // Stop any existing routine
        if (_activeCoroutine != null)
        {
            StopCoroutine(_activeCoroutine);
        }

        _activeCoroutine = StartCoroutine(MechTransformRoutine(skill));

        base.ExecuteSkill(@event);
    }

    private IEnumerator MechTransformRoutine(AegisProtocolSkillSO skill)
    {
        _isInMech = true;

        // === ENTER MECH ===
        EnterMech(skill);
        
        // Start cooldown IMMEDIATELY when entering mech, not when exiting
        // This prevents double cooldown time
        FinishSkill(skill);

        // Wait for mech duration
        yield return new WaitForSeconds(skill.mechDuration);

        // === EXIT MECH ===
        ExitMech(skill);
    }

    private void EnterMech(AegisProtocolSkillSO skill)
    {
        // Store original stats
        if (WorldInstance.Components.TryGet(EntityInstance, out MovementDataComponent movement))
        {
            _originalMoveSpeed = movement.MoveSpeed;
            // Apply speed penalty
            movement.MoveSpeed *= (1f - skill.moveSpeedPenalty);
        }

        // Increase attack range for mech (mech is bigger, needs larger hit detection)
        if (WorldInstance.Components.TryGet(EntityInstance, out WeaponDataComponent weapon))
        {
            _originalAttackRange = weapon.BaseRange;
            weapon.BaseRange *= 2f; // Double the range for mech attacks
            Debug.Log($"[AegisProtocol] Mech attack range increased: {_originalAttackRange} -> {weapon.BaseRange}");
        }

        if (WorldInstance.Components.TryGet(EntityInstance, out HealthDataComponent health))
        {
            _originalMaxHealth = health.MaxHealth;
            
            // Add bonus health as a shield
            _storedBonusHealth = health.MaxHealth * skill.healthBoostPercent;
            health.MaxHealth += _storedBonusHealth;
            health.CurrentHealth += _storedBonusHealth;

            // Set knockback immunity
            if (skill.knockbackImmune)
            {
                health.IsInvincible = false; // Not invincible, just immune to CC
                // Note: Actual knockback immunity should be checked in KnockbackSystem
            }
        }

        // Apply damage boost via buff event
        WorldInstance.Events.Publish(new ApplyBuffEvent
        {
            Target = EntityInstance,
            BuffType = BuffType.DamageBoost,
            Value = skill.damageBoostPercent,
            Duration = skill.mechDuration + 1f
        });

        // Swap models - server side
        SwapToMechModel(true);

        // Play enter VFX
        if (skill.enterMechVfxPrefab != null)
        {
            var vfx = Instantiate(skill.enterMechVfxPrefab, transform.position, Quaternion.identity, transform);
            vfx.Play();
            Destroy(vfx.gameObject, 3f);
        }

        // Play enter sound
        if (skill.enterMechSound != null)
        {
            AudioHelper.PlaySound3D(WorldInstance, skill.enterMechSound, AudioCategory.Player, transform.position);
        }

        // Start active mech VFX
        if (skill.activeMechVfxPrefab != null)
        {
            _activeMechVfx = Instantiate(skill.activeMechVfxPrefab, transform.position, Quaternion.identity, transform);
            _activeMechVfx.transform.localPosition = Vector3.zero;
            _activeMechVfx.Play();
        }

        // Start mech loop audio
        if (skill.mechActiveLoopSound != null)
        {
            _mechLoopAudio = gameObject.AddComponent<AudioSource>();
            _mechLoopAudio.clip = skill.mechActiveLoopSound;
            _mechLoopAudio.loop = true;
            _mechLoopAudio.volume = 0.4f;
            _mechLoopAudio.Play();
        }

        // Broadcast mech state to clients
        if (WorldInstance.Components.TryGet(EntityInstance, out NetworkSyncComponent sync))
        {
            sync.SyncView.BroadcastMechStateClientRpc(true);
        }
    }

    private void ExitMech(AegisProtocolSkillSO skill)
    {
        if (!_isInMech) return;

        _isInMech = false;
        _activeCoroutine = null;

        // Restore original stats
        if (WorldInstance.Components.TryGet(EntityInstance, out MovementDataComponent movement))
        {
            movement.MoveSpeed = _originalMoveSpeed;
        }

        // Restore attack range
        if (WorldInstance.Components.TryGet(EntityInstance, out WeaponDataComponent weapon))
        {
            weapon.BaseRange = _originalAttackRange;
        }

        if (WorldInstance.Components.TryGet(EntityInstance, out HealthDataComponent health))
        {
            // Remove bonus health, but don't reduce current health below 1
            health.MaxHealth = _originalMaxHealth;
            if (health.CurrentHealth > health.MaxHealth)
            {
                health.CurrentHealth = health.MaxHealth;
            }
        }

        // Swap models back
        SwapToMechModel(false);

        // Play exit VFX
        if (skill.exitMechVfxPrefab != null)
        {
            var vfx = Instantiate(skill.exitMechVfxPrefab, transform.position, Quaternion.identity);
            vfx.Play();
            Destroy(vfx.gameObject, 3f);
        }

        // Play exit sound
        if (skill.exitMechSound != null)
        {
            AudioHelper.PlaySound3D(WorldInstance, skill.exitMechSound, AudioCategory.Player, transform.position);
        }

        // Cleanup active VFX
        if (_activeMechVfx != null)
        {
            _activeMechVfx.Stop();
            Destroy(_activeMechVfx.gameObject, 1f);
            _activeMechVfx = null;
        }

        // Cleanup loop audio
        if (_mechLoopAudio != null)
        {
            _mechLoopAudio.Stop();
            Destroy(_mechLoopAudio);
            _mechLoopAudio = null;
        }

        // Broadcast mech state to clients
        if (WorldInstance.Components.TryGet(EntityInstance, out NetworkSyncComponent sync))
        {
            sync.SyncView.BroadcastMechStateClientRpc(false);
        }
    }

    private void ForceExitMech()
    {
        if (!_isInMech) return;

        _isInMech = false;
        _activeCoroutine = null;

        // Restore stats
        if (WorldInstance != null)
        {
            if (WorldInstance.Components.TryGet(EntityInstance, out MovementDataComponent movement))
            {
                // Only restore if we have a valid stored speed
                if (_originalMoveSpeed > 0)
                {
                    movement.MoveSpeed = _originalMoveSpeed;
                    _originalMoveSpeed = 0f; // Reset to prevent stale values on next mech use
                }
            }

            // Restore attack range
            if (WorldInstance.Components.TryGet(EntityInstance, out WeaponDataComponent weapon))
            {
                if (_originalAttackRange > 0)
                {
                    weapon.BaseRange = _originalAttackRange;
                    _originalAttackRange = 0f;
                }
            }

            if (WorldInstance.Components.TryGet(EntityInstance, out HealthDataComponent health))
            {
                health.MaxHealth = _originalMaxHealth;
            }
        }

        // Swap models back - use IMMEDIATE swap to avoid coroutine being cancelled during cleanup
        SwapToMechModelImmediate(false);

        // Cleanup VFX
        if (_activeMechVfx != null)
        {
            _activeMechVfx.Stop();
            Destroy(_activeMechVfx.gameObject);
            _activeMechVfx = null;
        }

        // Cleanup audio
        if (_mechLoopAudio != null)
        {
            _mechLoopAudio.Stop();
            Destroy(_mechLoopAudio);
            _mechLoopAudio = null;
        }

        // Broadcast state change to clients (CRITICAL for spectators/respawn)
        if (WorldInstance != null && WorldInstance.Components.TryGet(EntityInstance, out NetworkSyncComponent sync))
        {
            sync.SyncView.BroadcastMechStateClientRpc(false);
        }
    }

    /// <summary>
    /// Called when player respawns - ensures animator is reset to character form.
    /// </summary>
    private void OnPlayerRespawned(PlayerRespawnedEvent @event)
    {
        if (@event.Entity != EntityInstance) return;
        
        // Only server handles respawn logic
        if (!NetworkManager.Singleton.IsServer) return;
        
        // CRITICAL: Clear attack cache on respawn to ensure damage logic is reset
        // This fixes issue where Vex stops dealing damage to Boss after respawn if died mid-attack
        var attackView = GetComponent<AttackExecutionView>();
        if (attackView != null)
        {
            attackView.ClearDamageCache();
        }
        
        // Ensure we're in normal form with correct animator after respawn
        if (_isInMech)
        {
            ForceExitMech();
        }
        else
        {
            // Even if not in mech, ensure animator is set to character form
            // This handles cases where the mech state was cleared but animator wasn't reset
            EnsureCharacterAnimator();
            
            // CRITICAL: Broadcast to clients to ensure they also reset animator
            // Without this, clients/spectators may have stale animator state
            if (WorldInstance.Components.TryGet(EntityInstance, out NetworkSyncComponent sync))
            {
                sync.SyncView.BroadcastMechStateClientRpc(false);
            }
        }
    }

    /// <summary>
    /// Ensures the character animator is properly set after respawn or recovery.
    /// </summary>
    private void EnsureCharacterAnimator()
    {
        if (_animationView == null)
            _animationView = GetComponent<AnimationView>();
        
        if (_animationView != null && _characterAnimatorRef != null)
        {
            _characterAnimatorRef.Rebind();
            _characterAnimatorRef.Update(0);
            _animationView.SetAnimator(_characterAnimatorRef);
        }
        
        // Ensure models are in correct state
        if (_characterModel != null) _characterModel.SetActive(true);
        if (_mechModel != null) _mechModel.SetActive(false);
        if (_characterHealthBar != null) _characterHealthBar.SetActive(true);
        if (_mechHealthBar != null) _mechHealthBar.SetActive(false);
    }

    /// <summary>
    /// Immediately swaps models and animator without waiting a frame.
    /// Used during forced exits (death, destroy) where coroutines may be cancelled.
    /// </summary>
    private void SwapToMechModelImmediate(bool showMech)
    {
        // Reset attack state to prevent stuck IsAttacking flag after model swap
        if (WorldInstance != null)
        {
            if (WorldInstance.Components.TryGet(EntityInstance, out AttackDataComponent attack))
            {
                if (attack.IsAttacking)
                    attack.IsAttacking = false;
            }
            
            if (WorldInstance.Components.TryGet(EntityInstance, out CombatStateComponent state))
            {
                if (state.CurrentState == CombatState.Attacking)
                    state.CurrentState = CombatState.Idle;
            }

        }

        // Manually clear attack cache as ATTACK_END event may be missed during swap
        var attackView = GetComponent<AttackExecutionView>();
        if (attackView != null)
        {
            attackView.ClearDamageCache();
        }
        
        // Swap models
        if (showMech)
        {
            if (_mechModel != null) _mechModel.SetActive(true);
            if (_characterModel != null) _characterModel.SetActive(false);
        }
        else
        {
            if (_characterModel != null) _characterModel.SetActive(true);
            if (_mechModel != null) _mechModel.SetActive(false);
        }

        // Lazy init AnimationView
        if (_animationView == null)
            _animationView = GetComponent<AnimationView>();

        // IMMEDIATELY set animator - no coroutine delay
        Animator targetAnimator = showMech ? _mechAnimatorRef : _characterAnimatorRef;
        if (_animationView != null && targetAnimator != null)
        {
            targetAnimator.Rebind();
            targetAnimator.Update(0);
            _animationView.SetAnimator(targetAnimator);
        }

        // Toggle health bars
        if (_characterHealthBar != null) _characterHealthBar.SetActive(!showMech);
        if (_mechHealthBar != null) _mechHealthBar.SetActive(showMech);
    }

    private void SwapToMechModel(bool showMech)
    {
        // Reset attack state to prevent stuck IsAttacking flag after model swap
        // This is critical for clients where the ATTACK_END animation event may not fire
        // properly during animator transitions
        if (WorldInstance != null)
        {
            if (WorldInstance.Components.TryGet(EntityInstance, out AttackDataComponent attack))
            {
                if (attack.IsAttacking)
                    attack.IsAttacking = false;
            }
            
            if (WorldInstance.Components.TryGet(EntityInstance, out CombatStateComponent state))
            {
                if (state.CurrentState == CombatState.Attacking)
                    state.CurrentState = CombatState.Idle;
            }

        }

        // Manually clear attack cache as ATTACK_END event may be missed during swap
        var attackView = GetComponent<AttackExecutionView>();
        if (attackView != null)
        {
            attackView.ClearDamageCache();
        }
        
        // IMPORTANT: Enable the target model FIRST, then disable the other
        // This ensures animators are active before we try to reference them
        if (showMech)
        {
            // Entering mech: enable mech first, then disable character
            if (_mechModel != null)
            {
                _mechModel.SetActive(true);
            }
            if (_characterModel != null)
            {
                _characterModel.SetActive(false);
            }
        }
        else
        {
            // Exiting mech: enable character first, then disable mech
            if (_characterModel != null)
            {
                _characterModel.SetActive(true);
            }
            if (_mechModel != null)
            {
                _mechModel.SetActive(false);
            }
        }

        // Lazy init AnimationView if not cached yet (can happen on client if RPC arrives before Start)
        if (_animationView == null)
            _animationView = GetComponent<AnimationView>();

        // Swap animator reference with a frame delay to let Unity reinitialize the animator
        // This is crucial because animators on re-enabled GameObjects need a frame to fully initialize
        StartCoroutine(SwapAnimatorDelayed(showMech));
        
        // Toggle health bars - show the one for active form
        if (_characterHealthBar != null)
        {
            _characterHealthBar.SetActive(!showMech);
        }
        
        if (_mechHealthBar != null)
        {
            _mechHealthBar.SetActive(showMech);
        }
    }

    /// <summary>
    /// Swaps the animator reference after waiting one frame for Unity to reinitialize the animator.
    /// </summary>
    private IEnumerator SwapAnimatorDelayed(bool showMech)
    {
        // Wait one frame for animator to fully initialize after model was enabled
        yield return null;
        
        if (_animationView == null)
        {
            Debug.LogWarning("[AegisProtocol] AnimationView is null - cannot swap animator!");
            yield break;
        }

        // Use the pre-assigned SerializeField references directly
        Animator targetAnimator = showMech ? _mechAnimatorRef : _characterAnimatorRef;
        
        // Only set animator if we have a valid reference
        if (targetAnimator != null)
        {
            // Force animator to rebind all parameters - this is critical after model enable/disable
            // Without Rebind(), movement animations may not work correctly on client
            targetAnimator.Rebind();
            targetAnimator.Update(0);
            
            _animationView.SetAnimator(targetAnimator);
        }
        else
        {
            Debug.LogWarning($"[AegisProtocol] {(showMech ? "Mech" : "Character")} animator reference is null - check SerializeField assignment in inspector!");
        }
    }

    /// <summary>
    /// Called on clients to handle mech model swap
    /// </summary>
    public void OnMechStateChanged(bool isInMech)
    {
        SwapToMechModel(isInMech);

        // Client-side VFX for mech state change
        var skillBuffer = WorldInstance?.Components.Get<SkillCastBufferComponent>(EntityInstance);
        if (skillBuffer?.Skill is AegisProtocolSkillSO skill)
        {
            if (isInMech)
            {
                // Start active mech VFX on client
                if (skill.activeMechVfxPrefab != null && _activeMechVfx == null)
                {
                    _activeMechVfx = Instantiate(skill.activeMechVfxPrefab, transform.position, Quaternion.identity, transform);
                    _activeMechVfx.transform.localPosition = Vector3.zero;
                    _activeMechVfx.Play();
                }
            }
            else
            {
                // Cleanup VFX on client
                if (_activeMechVfx != null)
                {
                    _activeMechVfx.Stop();
                    Destroy(_activeMechVfx.gameObject, 1f);
                    _activeMechVfx = null;
                }
            }
        }
    }

    protected override void SpawnClientVisualEffect(SkillEffectTriggerEvent @event)
    {
        if (@event.Skill is not AegisProtocolSkillSO skill) return;

        StartCoroutine(ClientMechVisualRoutine(skill));
    }

    private IEnumerator ClientMechVisualRoutine(AegisProtocolSkillSO skill)
    {
        // Note: Main model swap is handled via BroadcastMechStateClientRpc -> OnMechStateChanged
        // This handles the initial enter VFX

        // Play enter VFX on client
        if (skill.enterMechVfxPrefab != null)
        {
            var vfx = Instantiate(skill.enterMechVfxPrefab, transform.position, Quaternion.identity, transform);
            vfx.Play();
            Destroy(vfx.gameObject, 3f);
        }

        // Client loop audio
        AudioSource clientAudio = null;
        if (skill.mechActiveLoopSound != null)
        {
            clientAudio = gameObject.AddComponent<AudioSource>();
            clientAudio.clip = skill.mechActiveLoopSound;
            clientAudio.loop = true;
            clientAudio.volume = 0.4f;
            clientAudio.spatialBlend = 1f;
            clientAudio.Play();
        }

        // Wait for duration
        yield return new WaitForSeconds(skill.mechDuration);

        // Play exit VFX on client
        if (skill.exitMechVfxPrefab != null)
        {
            var vfx = Instantiate(skill.exitMechVfxPrefab, transform.position, Quaternion.identity);
            vfx.Play();
            Destroy(vfx.gameObject, 3f);
        }

        // Cleanup audio
        if (clientAudio != null)
        {
            clientAudio.Stop();
            Destroy(clientAudio);
        }
    }

    protected override void OnDestroy()
    {
        if (WorldInstance != null)
        {
            WorldInstance.Events.Unsubscribe<EntityDeathEvent>(OnEntityDeath);
            WorldInstance.Events.Unsubscribe<PlayerRespawnedEvent>(OnPlayerRespawned);
        }

        StopAllCoroutines();
        ForceExitMech();
        base.OnDestroy();
    }
}
