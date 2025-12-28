using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Skill bar UI for spectators that displays the followed player's skills and cooldowns.
/// Dynamically updates when the spectator switches to follow a different player.
/// </summary>
public class SpectatorSkillBarUI : MonoBehaviour
{
    [SerializeField]
    private Transform skillContainer;

    [SerializeField]
    private SkillSlotUI skillSlotPrefab;

    [SerializeField]
    private GameObject noPlayerPanel; // Shows "Select a player to follow"

    private World _world;
    private SpectatorController _spectatorController;
    private EntityId _currentFollowedEntity;
    private readonly List<SkillSlotUI> _slots = new();

    private void Start()
    {
        StartCoroutine(InitializeWhenReady());
    }

    private System.Collections.IEnumerator InitializeWhenReady()
    {
        // Wait for WorldRunner
        while (WorldRunner.Instance == null || WorldRunner.Instance.World == null)
        {
            yield return null;
        }

        _world = WorldRunner.Instance.World;

        // Wait for SpectatorController
        while (SpectatorSpawner.Instance == null || !SpectatorSpawner.Instance.IsLocalSpectator)
        {
            yield return null;
        }

        _spectatorController = FindFirstObjectByType<SpectatorController>();
        if (_spectatorController == null)
        {
            Debug.LogError("[SpectatorSkillBarUI] Could not find SpectatorController!");
            yield break;
        }

        // Subscribe to events
        _world.Events.Subscribe<SkillEffectTriggerEvent>(OnSkillEffectTriggerEvent);
        _spectatorController.OnFollowedEntityChanged += OnFollowedEntityChanged;

        // Check if already following someone
        if (!_spectatorController.FollowedPlayerEntity.Equals(default))
        {
            OnFollowedEntityChanged(_spectatorController.FollowedPlayerEntity);
        }
        else
        {
            ShowNoPlayerMessage();
        }

        Debug.Log("[SpectatorSkillBarUI] Initialized");
    }

    private void Update()
    {
        foreach (var slot in _slots)
        {
            slot.UpdateCooldownVisual();
        }
    }

    private void OnFollowedEntityChanged(EntityId newEntity)
    {
        if (newEntity.Equals(_currentFollowedEntity))
        {
            return;
        }

        _currentFollowedEntity = newEntity;

        // Clear existing slots
        ClearSkillSlots();

        if (newEntity.Equals(default))
        {
            ShowNoPlayerMessage();
            return;
        }

        // Hide no player message
        if (noPlayerPanel != null)
        {
            noPlayerPanel.SetActive(false);
        }

        // Create slots for the new player's skills
        if (_world.Components.TryGet(newEntity, out SkillSetComponent skillSet))
        {
            CreateSkillSlots(skillSet.Skills);
            Debug.Log($"[SpectatorSkillBarUI] Showing skills for entity {newEntity.Id} ({skillSet.Skills.Count} skills)");
        }
        else
        {
            Debug.LogWarning($"[SpectatorSkillBarUI] Entity {newEntity.Id} has no SkillSetComponent");
        }
    }

    private void ClearSkillSlots()
    {
        foreach (var slot in _slots)
        {
            if (slot != null)
            {
                Destroy(slot.gameObject);
            }
        }
        _slots.Clear();
    }

    private void CreateSkillSlots(List<SkillDefinitionSO> skills)
    {
        if (skillSlotPrefab == null || skillContainer == null)
        {
            Debug.LogError("[SpectatorSkillBarUI] skillSlotPrefab or skillContainer not assigned!");
            return;
        }

        foreach (var skill in skills)
        {
            var slot = Instantiate(skillSlotPrefab, skillContainer);
            slot.Initialize(skill);
            _slots.Add(slot);
        }
    }

    private void ShowNoPlayerMessage()
    {
        if (noPlayerPanel != null)
        {
            noPlayerPanel.SetActive(true);
        }
    }

    private void OnSkillEffectTriggerEvent(SkillEffectTriggerEvent @event)
    {
        // Only respond to events for the currently followed player
        if (!@event.Caster.Equals(_currentFollowedEntity))
        {
            return;
        }

        int index = GetSkillIndex(@event.Skill);
        if (index >= 0 && index < _slots.Count)
        {
            _slots[index].TriggerCooldown(@event.Skill.cooldown);
            Debug.Log($"[SpectatorSkillBarUI] Triggered cooldown for {@event.Skill.skillName} ({@event.Skill.cooldown}s)");
        }
    }

    private int GetSkillIndex(SkillDefinitionSO skill)
    {
        for (int i = 0; i < _slots.Count; i++)
        {
            if (_slots[i].Skill == skill)
            {
                return i;
            }
        }
        return -1;
    }

    private void OnDestroy()
    {
        if (_world != null)
        {
            _world.Events.Unsubscribe<SkillEffectTriggerEvent>(OnSkillEffectTriggerEvent);
        }

        if (_spectatorController != null)
        {
            _spectatorController.OnFollowedEntityChanged -= OnFollowedEntityChanged;
        }
    }
}
