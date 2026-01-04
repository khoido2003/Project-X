using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Executor view for Vex's Explosive Drone skill (Q).
/// Server spawns a drone that flies toward the nearest enemy and explodes.
/// </summary>
public class SentryDroneExecutorView : SkillExecutorView
{
    public override SkillCategory Category => SkillCategory.SentryDrone;

    private EntityId _activeDroneEntity;
    private GameObject _activeDroneObject;
    private float _lastSpawnTime = -1f; // Guard against double execution
    
    // Client-side visual drone tracking
    private GameObject _clientDroneObject;
    private SentryDroneSkillSO _clientDroneSkill;
    private Coroutine _clientDroneCoroutine;
    private bool _waitingForServerExplosion;
    
    // Server position sync for client drone
    private Vector3 _serverDronePosition;
    private Quaternion _serverDroneRotation;
    private bool _hasServerPosition;

    protected override void Start()
    {
        base.Start();

        // Subscribe to death event to cleanup drone when player dies
        if (WorldInstance != null)
        {
            WorldInstance.Events.Subscribe<EntityDeathEvent>(OnEntityDeath);
        }
    }

    private void OnEntityDeath(EntityDeathEvent @event)
    {
        // Only cleanup if our entity died
        if (@event.Entity != EntityInstance) return;

        // Despawn any active drone
        DespawnActiveDrone();
    }

    protected override void ExecuteSkill(SkillConfirmExecutionEvent @event)
    {
        if (!NetworkManager.Singleton.IsServer)
        {
            return;
        }

        if (@event.Skill is not SentryDroneSkillSO skill)
        {
            return;
        }

        // Guard against double execution (can happen if skill is triggered via both SkillSystem and animation event)
        if (Time.time - _lastSpawnTime < 0.5f)
        {
            Debug.LogWarning("[SentryDroneExecutor] Blocked double execution within 0.5s");
            return;
        }
        _lastSpawnTime = Time.time;

        EntityViewRegistry registry = WorldInstance.Services.Resolve<EntityViewRegistry>();
        if (!registry.TryGet(@event.Caster, out EntityView casterView))
        {
            return;
        }

        // Despawn any existing drone first
        DespawnActiveDrone();

        // Calculate spawn position (in front of caster)
        Vector3 spawnPos = casterView.transform.position + casterView.transform.forward * 1.5f;
        spawnPos.y = casterView.transform.position.y + 1.5f;

        // Find initial target
        Vector3 targetPos = spawnPos + casterView.transform.forward * skill.detectionRange;
        EntityId targetEntity = FindNearestEnemy(spawnPos, skill.detectionRange, registry);

        if (!targetEntity.Equals(default) && registry.TryGet(targetEntity, out EntityView targetView))
        {
            targetPos = targetView.transform.position;
        }

        // Spawn the explosive drone
        SpawnExplosiveDrone(skill, spawnPos, targetPos, targetEntity, @event.Caster);

        base.ExecuteSkill(@event);
        FinishSkill(skill);
    }

    private EntityId FindNearestEnemy(Vector3 position, float range, EntityViewRegistry registry)
    {
        EntityId nearestEnemy = default;
        float nearestDistance = float.MaxValue;

        foreach (var (entity, _) in WorldInstance.Components.Query<EnemyComponent>())
        {
            // Skip dead enemies
            if (WorldInstance.Components.TryGet(entity, out HealthDataComponent health) && health.IsDead)
            {
                continue;
            }

            if (!registry.TryGet(entity, out EntityView enemyView))
            {
                continue;
            }

            float distance = Vector3.Distance(position, enemyView.transform.position);
            if (distance <= range && distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestEnemy = entity;
            }
        }

        return nearestEnemy;
    }

    private void SpawnExplosiveDrone(SentryDroneSkillSO skill, Vector3 position, Vector3 targetPos, EntityId targetEntity, EntityId owner)
    {
        GameObject droneObj;

        if (skill.dronePrefab != null)
        {
            droneObj = Instantiate(skill.dronePrefab, position, Quaternion.identity);
        }
        else
        {
            Debug.LogWarning("[SentryDroneExecutor] No drone prefab assigned! Creating placeholder.");
            droneObj = CreatePlaceholderDrone(position);
        }

        // Remove colliders to prevent physics interference with pathfinding
        RemoveAllColliders(droneObj);

        // Set layer to IgnoreRaycast
        droneObj.layer = LayerMask.NameToLayer("Ignore Raycast");
        foreach (Transform child in droneObj.GetComponentsInChildren<Transform>())
        {
            child.gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");
        }

        // NOTE: Drone is server-only - no NetworkObject needed
        // The drone gameplay logic (movement, explosion, damage) runs entirely on server
        // VFX for explosion is synced via RPC when drone explodes

        // Create entity in World - use temporary entity to avoid ID collision with players/enemies
        EntityId droneEntity = WorldInstance.CreateTemporaryEntity();

        // Get or add EntityView - prefab might not have it
        EntityView mainDroneView = droneObj.GetComponent<EntityView>();
        if (mainDroneView == null)
        {
            mainDroneView = droneObj.AddComponent<EntityView>();
            Debug.Log("[SentryDroneExecutor] Added EntityView to drone prefab");
        }
        mainDroneView.Bind(WorldInstance, droneEntity);
        Debug.Log($"[SentryDroneExecutor] Drone EntityView bound to entity {droneEntity.Id}");

        // Face toward target
        Vector3 direction = (targetPos - position).normalized;
        if (direction.sqrMagnitude > 0.01f)
        {
            droneObj.transform.rotation = Quaternion.LookRotation(direction);
        }

        // Add explosive drone component
        WorldInstance.Components.Add(droneEntity, new SentryDroneComponent
        {
            Owner = owner,
            TargetPosition = targetPos,
            TargetEntity = targetEntity,
            FlightSpeed = skill.flightSpeed,
            DetonationTime = Time.time + skill.maxLifetime,
            ExplosionRadius = skill.explosionRadius,
            ExplosionDamage = skill.explosionDamage,
            HasExploded = false,
            SkillData = skill,
            DroneView = mainDroneView
        });

        // Add transform component
        WorldInstance.Components.Add(droneEntity, new TransformComponent(position, droneObj.transform.rotation));

        // Drone is server-only, no network components needed

        _activeDroneEntity = droneEntity;
        _activeDroneObject = droneObj;

        // Play spawn VFX
        if (skill.spawnVfxPrefab != null)
        {
            var vfx = Instantiate(skill.spawnVfxPrefab, position, Quaternion.identity);
            vfx.Play();
            Destroy(vfx.gameObject, 2f);
        }

        // Play spawn sound
        if (skill.spawnSound != null)
        {
            AudioHelper.PlaySound3D(WorldInstance, skill.spawnSound, AudioCategory.Player, position);
        }

        // Start flying loop sound on drone
        if (skill.flyingLoopSound != null)
        {
            var audioSource = droneObj.AddComponent<AudioSource>();
            audioSource.clip = skill.flyingLoopSound;
            audioSource.loop = true;
            audioSource.volume = 0.5f;
            audioSource.spatialBlend = 1f;
            audioSource.Play();
        }

        Debug.Log($"[SentryDroneExecutor] Spawned explosive drone {droneEntity.Id} at {position}, target: {targetPos}");
    }

    private GameObject CreatePlaceholderDrone(Vector3 position)
    {
        GameObject drone = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        drone.name = "ExplosiveDrone_Placeholder";
        drone.transform.position = position;
        drone.transform.localScale = Vector3.one * 0.6f;

        var renderer = drone.GetComponent<Renderer>();
        if (renderer != null)
        {
            Material mat = new Material(Shader.Find("Standard"));
            mat.color = new Color(1f, 0.5f, 0f, 1f); // Orange for explosive
            mat.SetColor("_EmissionColor", new Color(1f, 0.3f, 0f));
            mat.EnableKeyword("_EMISSION");
            renderer.material = mat;
        }

        drone.AddComponent<EntityView>();
        return drone;
    }

    private void RemoveAllColliders(GameObject obj)
    {
        // Use Destroy instead of DestroyImmediate to avoid errors during animation callbacks
        foreach (var collider in obj.GetComponentsInChildren<Collider>(true))
        {
            Destroy(collider);
        }
    }

    private void DespawnActiveDrone()
    {
        if (_activeDroneEntity.Equals(default))
        {
            return;
        }

        if (_activeDroneObject != null)
        {
            var netObj = _activeDroneObject.GetComponent<NetworkObject>();
            if (netObj != null && netObj.IsSpawned)
            {
                netObj.Despawn(true);
            }
        }

        if (WorldInstance != null)
        {
            WorldInstance.DestroyEntity(_activeDroneEntity);
        }

        _activeDroneEntity = default;
        _activeDroneObject = null;
    }

    protected override void SpawnClientVisualEffect(SkillEffectTriggerEvent @event)
    {
        if (@event.Skill is not SentryDroneSkillSO skill) return;

        var registry = WorldInstance.Services.Resolve<EntityViewRegistry>();
        if (!registry.TryGet(@event.Caster, out EntityView casterView))
        {
            return;
        }

        Vector3 spawnPos = casterView.transform.position + casterView.transform.forward * 1.5f;
        spawnPos.y = casterView.transform.position.y + 1.5f;
        Vector3 direction = casterView.transform.forward;

        // Spawn VFX
        if (skill.spawnVfxPrefab != null)
        {
            var vfx = Instantiate(skill.spawnVfxPrefab, spawnPos, Quaternion.identity);
            vfx.Play();
            Destroy(vfx.gameObject, 2f);
        }

        // Spawn visual-only drone on client
        if (skill.dronePrefab != null)
        {
            // Guard: prevent double spawn if event fires twice
            if (_clientDroneObject != null)
            {
                Debug.LogWarning("[SentryDroneExecutor] Client drone already exists, destroying old one before spawning new");
                if (_clientDroneCoroutine != null)
                {
                    StopCoroutine(_clientDroneCoroutine);
                }
                Destroy(_clientDroneObject);
                _clientDroneObject = null;
            }
            
            _clientDroneCoroutine = StartCoroutine(ClientDroneVisualRoutine(skill, spawnPos, direction));
        }
    }

    /// <summary>
    /// Client-side visual-only drone that flies forward and explodes at the end.
    /// No gameplay logic, just visuals.
    /// </summary>
    private IEnumerator ClientDroneVisualRoutine(SentryDroneSkillSO skill, Vector3 startPos, Vector3 direction)
    {
        // Store skill reference for explosion
        _clientDroneSkill = skill;
        _waitingForServerExplosion = true;
        
        // Spawn visual drone
        GameObject droneObj = Instantiate(skill.dronePrefab, startPos, Quaternion.LookRotation(direction));
        _clientDroneObject = droneObj;
        
        // Remove colliders on client - drone is visual only
        foreach (var collider in droneObj.GetComponentsInChildren<Collider>())
        {
            Destroy(collider);
        }
        
        // Set layer to Ignore Raycast
        droneObj.layer = LayerMask.NameToLayer("Ignore Raycast");
        foreach (Transform child in droneObj.GetComponentsInChildren<Transform>())
        {
            child.gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");
        }
        
        // Play flying loop sound
        AudioSource audioSource = null;
        if (skill.flyingLoopSound != null)
        {
            audioSource = droneObj.AddComponent<AudioSource>();
            audioSource.clip = skill.flyingLoopSound;
            audioSource.loop = true;
            audioSource.volume = 0.5f;
            audioSource.spatialBlend = 1f;
            audioSource.Play();
        }
        
        // Fly forward until server tells us to explode (or timeout as fallback)
        // Use server position sync when available, otherwise hover near caster
        float elapsed = 0f;
        float maxFallbackTime = skill.maxLifetime + 2f; // Extra buffer time for network delay
        
        // Get caster view for fallback follow behavior
        var registry = WorldInstance.Services.Resolve<EntityViewRegistry>();
        registry.TryGet(EntityInstance, out EntityView casterView);
        
        // Reset server position tracking for new drone
        _hasServerPosition = false;
        
        while (elapsed < maxFallbackTime && droneObj != null && _waitingForServerExplosion)
        {
            Vector3 currentPos = droneObj.transform.position;
            
            // PRIORITY 1: Use server synced position if available
            if (_hasServerPosition)
            {
                // Interpolate toward server position for smooth movement
                Vector3 targetPos = _serverDronePosition;
                Vector3 moveDir = (targetPos - currentPos);
                
                float moveSpeed = skill.flightSpeed * 1.5f; // Faster to catch up with server
                float distance = moveDir.magnitude;
                
                if (distance > 0.1f)
                {
                    // Smooth interpolation toward server position
                    Vector3 newPos = Vector3.Lerp(currentPos, targetPos, Time.deltaTime * 8f);
                    droneObj.transform.position = newPos;
                    
                    // Face movement direction
                    if (moveDir.sqrMagnitude > 0.01f)
                    {
                        droneObj.transform.rotation = Quaternion.Slerp(
                            droneObj.transform.rotation,
                            _serverDroneRotation,
                            Time.deltaTime * 10f
                        );
                    }
                }
                else
                {
                    // Close enough - just use server position directly
                    droneObj.transform.position = targetPos;
                    droneObj.transform.rotation = _serverDroneRotation;
                }
            }
            // PRIORITY 2: Fallback - follow caster when no server sync
            else if (casterView != null)
            {
                // Follow caster - position slightly ahead and above
                Vector3 targetPos = casterView.transform.position + casterView.transform.forward * 2f + Vector3.up * 2f;
                Vector3 moveDir = (targetPos - currentPos);
                moveDir.y = Mathf.Clamp(moveDir.y, -0.5f, 0.5f);
                
                // Smooth follow with bobbing motion
                float moveSpeed = skill.flightSpeed * 0.7f;
                Vector3 newPos = currentPos + moveDir.normalized * moveSpeed * Time.deltaTime;
                newPos.y += Mathf.Sin(Time.time * 2f) * 0.02f; // Subtle bobbing
                droneObj.transform.position = newPos;
                
                // Face movement direction
                if (moveDir.sqrMagnitude > 0.01f)
                {
                    droneObj.transform.rotation = Quaternion.Slerp(
                        droneObj.transform.rotation,
                        Quaternion.LookRotation(moveDir.normalized),
                        Time.deltaTime * 10f
                    );
                }
            }
            else
            {
                // Fallback: just fly forward if we can't find caster
                droneObj.transform.position += direction * skill.flightSpeed * Time.deltaTime;
            }
            
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        // Only do fallback explosion if we timed out (server didn't send explosion RPC)
        if (droneObj != null && _waitingForServerExplosion)
        {
            Debug.LogWarning("[SentryDroneExecutor] Client drone timed out waiting for server explosion, using fallback");
            ExplodeClientDrone(droneObj.transform.position);
        }
    }
    
    /// <summary>
    /// Called by NetworkSyncView when server broadcasts drone explosion.
    /// </summary>
    public void OnDroneExplosionFromServer(Vector3 explosionPosition)
    {
        _waitingForServerExplosion = false;
        
        // Destroy client drone and play explosion at server-synced position
        ExplodeClientDrone(explosionPosition);
    }
    
    /// <summary>
    /// Explodes the client drone at the given position.
    /// </summary>
    private void ExplodeClientDrone(Vector3 position)
    {
        if (_clientDroneObject != null)
        {
            // Stop flying sound
            var audioSource = _clientDroneObject.GetComponent<AudioSource>();
            if (audioSource != null)
            {
                audioSource.Stop();
            }
            
            Destroy(_clientDroneObject);
            _clientDroneObject = null;
        }
        
        // Stop the coroutine if running
        if (_clientDroneCoroutine != null)
        {
            StopCoroutine(_clientDroneCoroutine);
            _clientDroneCoroutine = null;
        }
        
        // Play explosion VFX at synced position
        if (_clientDroneSkill != null && _clientDroneSkill.explosionVfxPrefab != null)
        {
            var vfx = Instantiate(_clientDroneSkill.explosionVfxPrefab, position, Quaternion.identity);
            vfx.Play();
            Destroy(vfx.gameObject, 2f);
        }
        
        // Play explosion sound at synced position
        if (_clientDroneSkill != null && _clientDroneSkill.explosionSound != null)
        {
            AudioHelper.PlaySound3D(WorldInstance, _clientDroneSkill.explosionSound, AudioCategory.Player, position);
        }
    }

    /// <summary>
    /// Called by NetworkSyncView when server syncs drone position.
    /// Updates client drone to follow server authoritative position.
    /// </summary>
    public void OnDronePositionFromServer(Vector3 position, Quaternion rotation)
    {
        _serverDronePosition = position;
        _serverDroneRotation = rotation;
        _hasServerPosition = true;
    }

    protected override void OnDestroy()
    {
        if (WorldInstance != null)
        {
            WorldInstance.Events.Unsubscribe<EntityDeathEvent>(OnEntityDeath);
        }

        base.OnDestroy();
    }
}
