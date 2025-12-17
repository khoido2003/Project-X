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

    private SkillVfxEffectSocket weaponVfxEffectSocket;
    private ParticleSystem skillVfxEffectInstance;

    private void Start()
    {
        weaponVfxEffectSocket = GetComponentInChildren<SkillVfxEffectSocket>();
    }

    public override void Bind(World world, EntityId entity)
    {
        base.Bind(world, entity);

        if (WorldInstance == null)
        {
            Debug.LogWarning("[SkillPreviewView] WorldInstance is null during Bind, subscriptions will be deferred");
            return;
        }

        try
        {
            WorldInstance.Events.Subscribe<MouseWorldInputEvent>(OnMouseWorldInputEvent);
            WorldInstance.Events.Subscribe<SkillPreviewRequestEvent>(OnSkillPreviewRequestEvent);
            WorldInstance.Events.Subscribe<SkillExecutionRequestEvent>(OnSkillExecutionRequestEvent);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[SkillPreviewView] Failed to subscribe to events during Bind: {ex.Message}");
        }
    }

    private void Update()
    {
        if (WorldInstance == null || EntityInstance.Id == 0)
        {
            return;
        }

        // Only local player shows preview
        if (!WorldInstance.Components.TryGet(EntityInstance, out NetworkOwnerComponent owner) || !owner.IsLocalPlayer)
        {
            return;
        }

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
        if (!WorldInstance.Components.TryGet(EntityInstance, out NetworkOwnerComponent owner) || !owner.IsLocalPlayer)
        {
            return;
        }
        if (@event.IsActive)
        {
            ShowPreview(@event.Skill);
            ShowSkillVfxEffect();
        }
        else
        {
            HideSkillVfxEffect();
            HidePreview();
        }
    }

    private void OnMouseWorldInputEvent(MouseWorldInputEvent @event)
    {
        mouseWorldPos = @event.MousePosition;
    }

    private void OnSkillExecutionRequestEvent(SkillExecutionRequestEvent @event)
    {
        // Clear SkillPreview flag when skill is executed
        if (WorldInstance.Components.TryGet(EntityInstance, out ActionFlagComponent flags))
        {
            flags.Set(ActionFlag.SkillPreview, false);
        }

        TryCastSkill();
        HideSkillVfxEffect();
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

        WorldInstance.Events.Publish(
            new EnterCombatStateEvent { Entity = EntityInstance, TargetState = CombatState.CastingSkill }
        );

        // WorldInstance.Events.Publish(
        //     new SkillEffectTriggerEvent
        //     {
        //         Caster = EntityInstance,
        //         Skill = skill,
        //         TargetPoint = target,
        //         Direction = direction,
        //     }
        // );

        if (!WorldInstance.Components.TryGet(EntityInstance, out SkillCastBufferComponent buffer))
        {
            buffer = new SkillCastBufferComponent();
            WorldInstance.Components.Add(EntityInstance, buffer);
        }

        buffer.Skill = skill;
        buffer.TargetPoint = target;
        buffer.Direction = direction;

        if (WorldInstance.Components.TryGet(EntityInstance, out NetworkSyncComponent sync))
        {
            sync.SyncView.RequestSkillExecutionServerRpc(target, direction);
        }

        HidePreview();
    }

    private void ShowSkillVfxEffect()
    {
        if (weaponVfxEffectSocket == null)
        {
            weaponVfxEffectSocket = GetComponentInChildren<SkillVfxEffectSocket>();

            if (weaponVfxEffectSocket == null)
            {
                Debug.LogWarning($"[{name}] No WeaponVfxEffectSocket found in children!");
                return;
            }
        }

        Transform vfxEffectSocketTransform = weaponVfxEffectSocket.GetSocket(SkillVfxEffectSocketName.CHARGE);

        if (vfxEffectSocketTransform == null)
        {
            return;
        }

        var currentSkill = GetCurrentSkill();

        if (currentSkill.skillVfxPrefab == null)
        {
            return;
        }

        skillVfxEffectInstance = Instantiate(
            currentSkill.skillVfxPrefab,
            vfxEffectSocketTransform.position,
            Quaternion.identity,
            vfxEffectSocketTransform
        );
    }

    private void HideSkillVfxEffect()
    {
        if (skillVfxEffectInstance != null)
        {
            skillVfxEffectInstance.Stop();
            Destroy(skillVfxEffectInstance.gameObject, 1f);
            skillVfxEffectInstance = null;
        }
    }

    private void OnDestroy()
    {
        HidePreview();
    }
}
