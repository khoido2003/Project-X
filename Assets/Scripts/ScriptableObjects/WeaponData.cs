using UnityEngine;

[CreateAssetMenu(fileName = "WeaponData", menuName = "Character/WeaponData")]
public class WeaponData : ScriptableObject
{
    [Header("Configs")]
    public string weaponName;
    public bool isMelee = true;

    [Header("Stats")]
    public float attackDamage = 10f;
    public float attackCooldown = 0.5f;
    public float attackRange = 1f;

    [Header("Visuals")]
    public GameObject weaponPrefab;
    public GameObject projectilePrefab;
    public Vector3 spawnPositionOffset = Vector3.zero;
    public Vector3 spawnRotationOffset = Vector3.zero;

    [Header("Sounds and Animations")]
    public AudioClip attackSound;

    public string attackAnimationTrigger = "attack";
    public int totalAttackAnimations = 2;
}
