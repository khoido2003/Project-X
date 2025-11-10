using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SkillSlotUI : MonoBehaviour
{
    [SerializeField]
    private Image iconImage;

    [SerializeField]
    private Image cooldownFill;

    [SerializeField]
    private SelectedSkill selectedSkill;

    [SerializeField]
    private TextMeshProUGUI keyTrigger;

    public SkillDefinitionSO Skill { get; private set; }

    private float _cooldownEndTime;
    private float _cooldownDuration;

    public void Initialize(SkillDefinitionSO skill)
    {
        Skill = skill;
        cooldownFill.fillAmount = 0f;

        selectedSkill.gameObject.SetActive(false);
        keyTrigger.text = skill.keyTrigger;
    }

    public void TriggerCooldown(float duration)
    {
        _cooldownDuration = duration;
        _cooldownEndTime = Time.time + duration;
    }

    public void UpdateCooldownVisual()
    {
        if (_cooldownEndTime <= 0f)
        {
            return;
        }

        float remaining = _cooldownEndTime - Time.time;
        if (remaining <= 0f)
        {
            cooldownFill.fillAmount = 0f;
            _cooldownEndTime = 0f;
        }
        else
        {
            cooldownFill.fillAmount = remaining / _cooldownDuration;
        }
    }

    public void SetSelected(bool pressed)
    {
        selectedSkill.gameObject.SetActive(pressed);
    }
}
