using System;
using UnityEngine;

public class SkillPreviewView : EntityView
{
    [Header("Visual Settings")]
    [SerializeField]
    private int ringSegments = 64;

    [SerializeField]
    private float ringWidth = 0.05f;

    [SerializeField]
    private Color rangeColor = Color.blue;

    [SerializeField]
    private Color indicatorColor = Color.orangeRed;

    [SerializeField]
    private float indicatorRadius = 0.5f;

    private LineRenderer rangeRing;
    private LineRenderer indicatorRing;
    private int selectedSkillIndex = -1;

    private Vector3 mouseWorldPos;

    public override void Bind(World world, EntityId entity)
    {
        base.Bind(world, entity);

        WorldInstance.Events.Subscribe<MouseWorldInputEvent>(OnMouseWorldInputEvent);

        WorldInstance.Events.Subscribe<SkillPreviewRequestEvent>(OnSkillPreviewRequestEvent);

        WorldInstance.Events.Subscribe<SkillExecutionRequestEvent>(OnSkillExecutionRequestEvent);
    }

    private void Update()
    {
        if (selectedSkillIndex == -1)
        {
            return;
        }

        UpdateRangeVisuals();
    }

    private void OnSkillPreviewRequestEvent(SkillPreviewRequestEvent @event)
    {
        if (@event.Entity != EntityInstance)
        {
            return;
        }

        if (@event.IsActive)
        {
            ShowPreview(@event.Skill);
        }
        else
        {
            HidePreview();
        }
    }

    private void OnMouseWorldInputEvent(MouseWorldInputEvent @event)
    {
        mouseWorldPos = @event.MousePosition;
    }

    private void OnSkillExecutionRequestEvent(SkillExecutionRequestEvent @event)
    {
        TryCastSkill();
    }

    private void ShowPreview(SkillDefinitionSO skill)
    {
        HidePreview();

        // Range
        rangeRing = Drawer.CreateCircle(skill.castRange, ringSegments, ringWidth, rangeColor);
        rangeRing.transform.position = transform.position + Vector3.up * 0.05f;

        // Indicator
        indicatorRing = Drawer.CreateCircle(indicatorRadius, ringSegments, ringWidth, indicatorColor);

        Vector3 target = mouseWorldPos;
        indicatorRing.transform.position = target + Vector3.up * 0.05f;

        selectedSkillIndex = FindSkillIndex(skill);
    }

    private void HidePreview()
    {
        selectedSkillIndex = -1;

        if (indicatorRing)
        {
            Destroy(indicatorRing.gameObject);
        }

        if (rangeRing)
        {
            Destroy(rangeRing.gameObject);
        }
    }

    private int FindSkillIndex(SkillDefinitionSO skill)
    {
        if (WorldInstance.Components.TryGet(EntityInstance, out SkillSetComponent skillSet))
        {
            for (int i = 0; i < skillSet.Skills.Count; i++)
            {
                if (skillSet.Skills[i] == skill)
                {
                    return i;
                }
            }
        }

        return -1;
    }

    private SkillDefinitionSO GetCurrentSkill()
    {
        if (selectedSkillIndex == -1)
        {
            return null;
        }

        if (WorldInstance.Components.TryGet(EntityInstance, out SkillSetComponent skillSet))
        {
            return skillSet.Skills[selectedSkillIndex];
        }

        return null;
    }

    private void UpdateRangeVisuals()
    {
        if (indicatorRing == null)
        {
            return;
        }

        Vector3 target = mouseWorldPos;
        float range = GetCurrentSkill()?.castRange ?? 0f;

        Vector3 dir = (target - transform.position).normalized;
        dir.y = 0;

        if (Vector3.Distance(transform.position, target) > range)
        {
            target = transform.position + dir * range;
        }

        indicatorRing.transform.position = target + Vector3.up * 0.05f;

        if (rangeRing != null)
        {
            rangeRing.transform.position = transform.position + Vector3.up * 0.05f;
        }
    }

    private void TryCastSkill()
    {
        if (!WorldInstance.Components.TryGet(EntityInstance, out CombatStateComponent state))
        {
            return;
        }

        if (state.CurrentState == CombatState.Attacking)
        {
            return;
        }

        SkillDefinitionSO skill = GetCurrentSkill();
        if (skill == null)
        {
            return;
        }

        Vector3 target = mouseWorldPos;
        float range = GetCurrentSkill()?.castRange ?? 0f;

        Vector3 dir = (target - transform.position).normalized;
        dir.y = 0;

        if (Vector3.Distance(transform.position, target) > range)
        {
            target = transform.position + dir * range;
        }

        Vector3 direction = dir.normalized;

        state.CurrentState = CombatState.CastingSkill;
        state.LastActionTime = Time.time;

        WorldInstance.Events.Publish(
            new SkillConfirmExecutionEvent
            {
                Caster = EntityInstance,
                Skill = skill,
                TargetPoint = target,
                Direction = direction,
            }
        );
        HidePreview();
    }

    private void OnDestroy()
    {
        HidePreview();
    }
}
