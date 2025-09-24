using System;
using UnityEngine;

[RequireComponent(typeof(MouseWorldPosition))]
public class SkillComponent : MonoBehaviour, IAnimationTrigger
{
    private SkillInstance[] skills = new SkillInstance[3];
    private bool isPlayer;
    private Character character;
    private MouseWorldPosition mouseWorldPosition;

    private LineRenderer rangeRing;
    private LineRenderer indicatorRing;
    private int selectedSkillIndex = -1;

    [Header("Visual Settings")]
    [SerializeField]
    private int ringSegments = 64;

    [SerializeField]
    private float ringWidth = 0.05f;

    [SerializeField]
    private Color rangeColor = Color.blue;

    [SerializeField]
    private Color indicatorColor = Color.green;

    [SerializeField]
    private float indicatorRadius = 0.5f;

    public event Action<string> OnTriggerAnimation;
    public event Action<string, float> OnSetFloatParameter;
    public event Action<string, bool> OnSetBoolParameter;

    void Update()
    {
        if (selectedSkillIndex != -1)
            UpdateVisuals();
    }

    public void Initialize(SkillData[] skillDatas, bool isPlayerControlled)
    {
        character = GetComponent<Character>();
        mouseWorldPosition = GetComponent<MouseWorldPosition>();
        isPlayer = isPlayerControlled;

        for (int i = 0; i < skills.Length; i++)
        {
            if (skillDatas != null && i < skillDatas.Length && skillDatas[i] != null)
            {
                skills[i] = new SkillInstance(skillDatas[i]);
            }
        }

        if (isPlayer)
        {
            InputManager.Instance.OnSkill1Pressed += () => SelectSkill(0);
            InputManager.Instance.OnSkill2Pressed += () => SelectSkill(1);
            InputManager.Instance.OnSkill3Pressed += () => SelectSkill(2);
            InputManager.Instance.OnSkill1Released += () => CancelSkill(0);
            InputManager.Instance.OnSkill2Released += () => CancelSkill(1);
            InputManager.Instance.OnSkill3Released += () => CancelSkill(2);
            InputManager.Instance.OnAttackPressed += CastSelectedSkill;
        }
    }

    private void SelectSkill(int index)
    {
        if (skills[index] == null || !skills[index].CanUse())
        {
            return;
        }
        selectedSkillIndex = index;

        // Range ring
        rangeRing = Drawer.CreateCircle(skills[index].Data.castRange, ringSegments, ringWidth, rangeColor);
        rangeRing.transform.position = transform.position + Vector3.up * 0.05f;

        // Target indicator
        indicatorRing = Drawer.CreateCircle(indicatorRadius, ringSegments, ringWidth, indicatorColor);
    }

    private void CancelSkill(int index)
    {
        if (selectedSkillIndex == index)
        {
            HideVisuals();
        }
    }

    private void CastSelectedSkill()
    {
        if (selectedSkillIndex == -1)
        {
            return;
        }
        UseSkill(selectedSkillIndex);
        HideVisuals();
    }

    private void UseSkill(int index)
    {
        if (skills[index] == null || mouseWorldPosition == null)
            return;

        Vector3 target = mouseWorldPosition.GetWorldPosition();
        float range = skills[index].Data.castRange;

        Vector3 directionToTarget = (target - transform.position).normalized;
        directionToTarget.y = 0;

        if (Vector3.Distance(transform.position, target) > range)
        {
            target = transform.position + directionToTarget * range;
        }

        directionToTarget *= character?.Data?.forwardDirectionMultiplier ?? 1f;

        OnTriggerAnimation?.Invoke(skills[index]?.Data.activationAnimationTrigger ?? "skill");

        skills[index].Use(gameObject, target, directionToTarget);
    }

    private void UpdateVisuals()
    {
        if (indicatorRing == null || mouseWorldPosition == null)
        {
            return;
        }

        Vector3 target = mouseWorldPosition.GetWorldPosition();
        float range = skills[selectedSkillIndex].Data.castRange;

        Vector3 directionToTarget = (target - transform.position).normalized;
        directionToTarget.y = 0;

        if (Vector3.Distance(transform.position, target) > range)
        {
            target = transform.position + directionToTarget * range;
        }

        indicatorRing.transform.position = target + Vector3.up * 0.05f;

        if (rangeRing != null)
        {
            rangeRing.transform.position = transform.position + Vector3.up * 0.05f;
        }
    }

    private void HideVisuals()
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

    private void OnDestroy()
    {
        if (InputManager.Instance != null)
        {
            InputManager.Instance.OnSkill1Pressed -= () => SelectSkill(0);
            InputManager.Instance.OnSkill2Pressed -= () => SelectSkill(1);
            InputManager.Instance.OnSkill3Pressed -= () => SelectSkill(2);
            InputManager.Instance.OnSkill1Released -= () => CancelSkill(0);
            InputManager.Instance.OnSkill2Released -= () => CancelSkill(1);
            InputManager.Instance.OnSkill3Released -= () => CancelSkill(2);
            InputManager.Instance.OnAttackPressed -= CastSelectedSkill;
        }
        HideVisuals();
    }
}
