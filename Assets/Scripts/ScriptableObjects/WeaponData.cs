using UnityEngine;

[CreateAssetMenu(fileName = "WeaponData", menuName = "Character/WeaponData")]
public class WeaponData : ScriptableObject
{
    public string weaponName;
    public bool isMelee = true;
    public float attackDamage = 10f;
    public float attackCooldown = 0.5f;
    public float attackRange = 1f;

    public GameObject weaponPrefab;
    public GameObject projectilePrefab;
    public Vector3 spawnPositionOffset = Vector3.zero;
    public Vector3 spawnRotationOffset = Vector3.zero;

    public AudioClip attackSound;
    public string attackAnimationTrigger = "Attack";
}
