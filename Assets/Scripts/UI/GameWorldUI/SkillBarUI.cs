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
    }

    private void Update()
    {
        foreach (var slot in _slots)
            slot.UpdateCooldownVisual();
    }

    private void CreateSkillSlots(List<SkillDefinitionSO> skills)
    {
        foreach (var skill in skills)
        {
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
        for (int i = 0; i < _slots.Count; i++)
        {
            if (_slots[i].Skill == skill)
            {
                return i;
            }
        }
        return -1;
    }

    // Called on CLIENT when skill effect is triggered via RPC - this triggers cooldown on client
    private void OnSkillEffectTriggerEvent(SkillEffectTriggerEvent @event)
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

    private void OnDestroy()
    {
        if (_world != null)
        {
            _world.Events.Unsubscribe<SkillExecutionFinishedEvent>(OnSkillExecutionFinishedEvent);
            _world.Events.Unsubscribe<SkillPressedInputEvent>(OnSkillPressedInputEvent);
            _world.Events.Unsubscribe<SkillEffectTriggerEvent>(OnSkillEffectTriggerEvent);
        }
    }
}
