// CharacterCardUI.cs
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

    private CharacterDefinitionSO character;

    public void Setup(CharacterDefinitionSO character)
    {
        this.character = character;
        characterName.text = character.characterName;
        health.text = $"HP: {character.maxHealth}";

        selectBtn.onClick.RemoveAllListeners();
        selectBtn.onClick.AddListener(OnSelect);
    }

    private void OnSelect()
    {
        UIManager.Instance?.OnCharacterSelected(character);
    }
}
