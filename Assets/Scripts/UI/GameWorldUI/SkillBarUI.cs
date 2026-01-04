using System.Collections.Generic;
using UnityEngine;

public class SkillBarUI : MonoBehaviour
{
    [SerializeField]
    private Transform skillContainer;

    [SerializeField]
    private SkillSlotUI skillSlotPrefab;

    private World _world;
    private EntityId _playerEntity;
    private readonly List<SkillSlotUI> _slots = new();

    public void Bind(World world)
    {
        _world = world;
        
        _world.Events.Subscribe<SkillExecutionFinishedEvent>(OnSkillExecutionFinishedEvent);
        _world.Events.Subscribe<SkillPressedInputEvent>(OnSkillPressedInputEvent);
        _world.Events.Subscribe<SkillEffectTriggerEvent>(OnSkillEffectTriggerEvent);
        _world.Events.Subscribe<GamePhaseChangedEvent>(OnGamePhaseChangedEvent);

        // Find LOCAL player entity - important on client where there are multiple players
        foreach (var (entity, owner) in _world.Components.Query<NetworkOwnerComponent>())
        {
            if (owner.IsLocalPlayer && _world.Components.Has<PlayerTagComponent>(entity))
            {
                _playerEntity = entity;
                break;
            }
        }

        if (_playerEntity.Equals(default))
        {
            Debug.LogError("[SkillBarUI] Failed to find local player entity!");
            return;
        }

        if (_world.Components.TryGet(_playerEntity, out SkillSetComponent skillSet))
        {
            CreateSkillSlots(skillSet.Skills);
        }
        else
        {
            Debug.LogError("[SkillBarUI] Player has no SkillSetComponent!");
        }
    }

    private void Update()
    {
        foreach (var slot in _slots)
            slot.UpdateCooldownVisual();
    }

    private void CreateSkillSlots(List<SkillDefinitionSO> skills)
    {
        for (int i = 0; i < skills.Count; i++)
        {
            var skill = skills[i];
            var slot = Instantiate(skillSlotPrefab, skillContainer);
            slot.Initialize(skill);
            _slots.Add(slot);
        }
    }

    private void OnSkillPressedInputEvent(SkillPressedInputEvent @event)
    {
        if (!_playerEntity.Equals(@event.Entity))
        {
            return;
        }

        int index = @event.SkillIndex - 1;
        if (index >= 0 && index < _slots.Count)
        {
            _slots[index].SetSelected(@event.IsPressed);
        }
    }

    private void OnSkillExecutionFinishedEvent(SkillExecutionFinishedEvent @event)
    {
        if (!_playerEntity.Equals(@event.Caster))
        {
            return;
        }

        int index = GetSkillIndex(@event.Skill);
        if (index >= 0 && index < _slots.Count)
        {
            _slots[index].TriggerCooldown(@event.Skill.cooldown);
        }
    }

    private int GetSkillIndex(SkillDefinitionSO skill)
    {
        if (skill == null)
        {
            return -1;
        }
        
        for (int i = 0; i < _slots.Count; i++)
        {
            // Match by category since object references might differ between server/client
            if (_slots[i].Skill != null && _slots[i].Skill.category == skill.category)
            {
                return i;
            }
        }
        
        return -1;
    }

    // Called on CLIENT when skill effect is triggered via RPC - this triggers cooldown on client
    private void OnSkillEffectTriggerEvent(SkillEffectTriggerEvent @event)
    {
        // Check if this is for our player
        if (!_playerEntity.Equals(@event.Caster))
        {
            return;
        }

        int index = GetSkillIndex(@event.Skill);
        if (index >= 0 && index < _slots.Count)
        {
            _slots[index].TriggerCooldown(@event.Skill.cooldown);
        }
    }

    private void OnGamePhaseChangedEvent(GamePhaseChangedEvent @event)
    {
        // Hide skill bar on game end
        if (@event.Phase == GamePhase.GameEnd)
        {
            if (skillContainer != null)
                skillContainer.gameObject.SetActive(false);
        }
        else if (!skillContainer.gameObject.activeSelf)
        {
            // Show it again in other phases (like new lobby or restart)
            skillContainer.gameObject.SetActive(true);
        }
    }

    private void OnDestroy()
    {
        if (_world != null)
        {
            _world.Events.Unsubscribe<SkillExecutionFinishedEvent>(OnSkillExecutionFinishedEvent);
            _world.Events.Unsubscribe<SkillPressedInputEvent>(OnSkillPressedInputEvent);
            _world.Events.Unsubscribe<SkillEffectTriggerEvent>(OnSkillEffectTriggerEvent);
            _world.Events.Unsubscribe<GamePhaseChangedEvent>(OnGamePhaseChangedEvent);
        }
    }
}
