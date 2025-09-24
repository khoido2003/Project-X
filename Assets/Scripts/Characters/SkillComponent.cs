using System;
using UnityEngine;

public class SkillComponent : MonoBehaviour, IAnimationTrigger
{
    private SkillInstance[] skills = new SkillInstance[3];
    private bool isPlayer;
    private Character character;
    private MouseWorldPosition mouseWorldPosition;

    public event Action<string> OnTriggerAnimation;
    public event Action<string, float> OnSetFloatParameter;
    public event Action<string, bool> OnSetBoolParameter;

    public void Initialize(SkillData[] skillDatas, bool isPlayerControlled)
    {
        character = GetComponent<Character>();
        mouseWorldPosition = GetComponent<MouseWorldPosition>();

        isPlayer = isPlayerControlled;

        for (int i = 0; i < 3; i++)
        {
            if (skillDatas != null && i < skillDatas.Length && skillDatas[i] != null)
            {
                skills[i] = new SkillInstance(skillDatas[i]);
            }
        }

        if (isPlayer)
        {
            InputManager.Instance.OnSkill1Pressed += () => UseSkill(0);
            InputManager.Instance.OnSkill2Pressed += () => UseSkill(1);
            InputManager.Instance.OnSkill3Pressed += () => UseSkill(2);
        }
    }

    private void UseSkill(int index)
    {
        if (index < 0 || index >= 3 || skills[index] == null)
        {
            Debug.LogError($"Invalid or unassigned skill index {index}!");
            return;
        }

        if (!skills[index].CanUse())
        {
            return;
        }

        if (mouseWorldPosition == null)
        {
            Debug.LogError("Missing MouseWorldPosition Component!");
            return;
        }

        Vector3 targetPoint = mouseWorldPosition.GetWorldPosition();
        float castRange = skills[index].Data.castRange;

        Vector3 direction = (targetPoint - transform.position).normalized;
        direction.y = 0f;

        float distance = Vector3.Distance(targetPoint, transform.position);

        if (distance > castRange)
        {
            targetPoint = transform.position + direction * castRange;
        }

        direction *= character?.Data?.forwardDirectionMultiplier ?? 1f;

        // TRIGGER ANIMATION HERE
        OnTriggerAnimation?.Invoke(skills[index]?.Data.activationAnimationTrigger ?? "skill");

        // TODO: Trigger sound here


        // Start using skill
        skills[index].Use(gameObject, targetPoint, direction);
    }
}
