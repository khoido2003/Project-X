using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "CharacterDefinition", menuName = "Game/Character Defintion")]
public class CharacterDefinitionSO : ScriptableObject
{
    [Header("General Info")]
    public string characterName;
    public GameObject prefab;
    public bool isPlayer;

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
}
