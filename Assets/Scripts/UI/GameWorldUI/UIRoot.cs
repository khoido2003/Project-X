using System.Collections;
using UnityEngine;

public class UIRoot : MonoBehaviour
{
    [SerializeField]
    private HealthHUD_UI healthHUD;

    [SerializeField]
    private SkillBarUI skillBarUI;

    private World _world;

    private bool _isInitialized = false;

    private void Start()
    {
        StartCoroutine(InitializeWhenReady());
    }

    private IEnumerator InitializeWhenReady()
    {
        // Wait for WorldRunner and World to be ready
        while (WorldRunner.Instance == null || WorldRunner.Instance.World == null)
        {
            yield return null;
        }

        // Wait one more frame to ensure all entities are spawned
        yield return null;

        World world = WorldRunner.Instance.World;

        // Wait for local player to be spawned
        EntityId localPlayer = default;
        int attempts = 0;
        while (localPlayer.Equals(default) && attempts < 100) // 10 seconds max wait
        {
            foreach (var (entity, owner) in world.Components.Query<NetworkOwnerComponent>())
            {
                if (owner.IsLocalPlayer && world.Components.Has<PlayerTagComponent>(entity))
                {
                    localPlayer = entity;
                    break;
                }
            }

            if (localPlayer.Equals(default))
            {
                attempts++;
                yield return new WaitForSeconds(0.1f);
            }
        }

        if (localPlayer.Equals(default))
        {
            Debug.LogError("[GameUIManager] Failed to find local player entity!");
            yield break;
        }

        // Initialize UIs
        if (healthHUD != null)
        {
            healthHUD.Bind(world);
            Debug.Log("[GameUIManager] HealthHUD initialized");
        }

        if (skillBarUI != null)
        {
            skillBarUI.Bind(world);
            Debug.Log("[GameUIManager] SkillBarUI initialized");
        }

        _isInitialized = true;
        Debug.Log("[GameUIManager] All UI components initialized successfully");
    }
}
