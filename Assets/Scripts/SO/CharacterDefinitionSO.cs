using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "CharacterDefinition", menuName = "Game/Character Defintion")]
public class CharacterDefinitionSO : NetworkSO
{
    [Header("General Info")]
    public string characterName;
    public GameObject prefab;
    public bool isPlayer;
    public Sprite characterSprite;
    public Sprite weaponSprite;

    [Header("Health Stats")]
    public float maxHealth = 102f;

    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float forwardMultiplier = 3f;

    [Header("Animation Settings")]
    public string isMovingParam = "isMoving";
    public string moveXParam = "moveX";
    public string moveYParam = "moveY";
    public string attackAnimationTrigger = "attack";
    public int totalAttackAnimations = 4;

    [Header("Attacks")]
    public List<AttackDefinition> attacks = new();

    [Header("Skills")]
    public List<SkillDefinitionSO> skills = new();

    [Header("Client Info")]
    public ulong clientId;

    public int playerId;

    public bool isSelected;

    [Header("Score")]
    public int enemiesDestroyed;

    public long totalDamages;

    private void Enable()
    {
        EmptyData();
    }

    public void EmptyData()
    {
        isSelected = false;
        clientId = 0;
        playerId = -1;
        enemiesDestroyed = 0;
        totalDamages = 0;
    }
}
