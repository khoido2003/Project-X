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
    public float maxHealth = 101f;

    [Header("Movement Settings")]
    public float moveSpeed = 4f;
    public float forwardMultiplier = 2f;

    [Header("Animation Settings")]
    public string isMovingParam = "isMoving";
    public string moveXParam = "moveX";
    public string moveYParam = "moveY";

    [Header("Weapon Settings")]
    public bool hasWeapon = true;
    public WeaponData weaponData;

    [System.Serializable]
    public class WeaponData
    {
        [Header("Config")]
        public string weaponName;
        public AttackExecutionType ExecutionType;

        [Header("Stats")]
        public float attackDamage = 11f;
        public float attackCooldown = 1.5f;
        public float attackRange = 2f;

        [Header("Visuals")]
        public ParticleSystem hitImpactParticlePrefab;

        [Header("Animation & Audio")]
        public string attackAnimationTrigger = "attack";
        public int totalAttackAnimations = 3;
        public AudioClip attackSound;
    }

    [Header("Skills")]
    public List<SkillDefinitionSO> skills = new();
}
