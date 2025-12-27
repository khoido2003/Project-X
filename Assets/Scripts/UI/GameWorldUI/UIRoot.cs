using System.Collections;
using UnityEngine;

public class UIRoot : MonoBehaviour
{
    [SerializeField]
    private HealthHUD_UI healthHUD;

    [SerializeField]
    private SkillBarUI skillBarUI;

    [Header("Spectator Mode")]
    [SerializeField]
    [Tooltip("Root object containing player-specific UI (hide for spectators)")]
    private GameObject playerUIRoot;

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

        // Check if we're a spectator
        bool isSpectator = ConnectionSettings.IsSpectator;

        if (isSpectator)
        {
            // Spectator mode - hide player-specific UI
            Debug.Log("[UIRoot] Spectator mode - hiding player UI");

            if (playerUIRoot != null)
            {
                playerUIRoot.SetActive(false);
            }
            else
            {
                // Fallback: hide individual elements
                if (healthHUD != null)
                    healthHUD.gameObject.SetActive(false);
                if (skillBarUI != null)
                    skillBarUI.gameObject.SetActive(false);
            }

            _isInitialized = true;
            yield break;
        }

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

        // Wait for skills to be populated via RPC
        // On clients, SkillSetComponent is initially empty and populated by SyncCharacterDataClientRpc
        if (world.Components.TryGet(localPlayer, out SkillSetComponent skillSet))
        {
            int skillWaitAttempts = 0;
            while (skillSet.Skills.Count == 0 && skillWaitAttempts < 50) // 5 seconds max wait
            {
                skillWaitAttempts++;
                yield return new WaitForSeconds(0.1f);
            }

            if (skillSet.Skills.Count == 0)
            {
                Debug.LogWarning("[GameUIManager] Skills not synced after 5 seconds, proceeding anyway");
            }
        }

        // Initialize UIs
        if (healthHUD != null)
        {
            healthHUD.Bind(world);
        }

        if (skillBarUI != null)
        {
            skillBarUI.Bind(world);
        }

        _isInitialized = true;
    }
}
