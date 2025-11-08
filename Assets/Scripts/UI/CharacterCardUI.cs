using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CharacterCardUI : MonoBehaviour
{
    [SerializeField]
    private Image characterImage;

    [SerializeField]
    private TextMeshProUGUI characterName;

    [SerializeField]
    private TextMeshProUGUI health;

    [SerializeField]
    private Button selectBtn;

    public void Setup(CharacterDefinitionSO character)
    {
        characterName.text = character.characterName;
        health.text = $"HP: {character.maxHealth}";

        selectBtn.onClick.RemoveAllListeners();
        selectBtn.onClick.AddListener(() => OnCharacterSelected(character));
    }

    private void OnCharacterSelected(CharacterDefinitionSO character)
    {
        if (UIManager.Instance != null)
        {
            UIManager.Instance.OnCharacterSelected(character);
        }
    }
}
