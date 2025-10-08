using UnityEngine;

[CreateAssetMenu(menuName = "Game/Character SO", fileName = "NewCharacterData")]
public class CharacterSO : ScriptableObject
{
    [Header("General")]
    public string characterName;
    public GameObject prefab;
    public bool isPlayer;

    [Header("Stats")]
    public float moveSpeed = 3f;
    public float forwardMultiplier = 1f;

    [Header("Animation")]
    public string isMoving = "isMoving";
    public string moveX = "moveX";
    public string moveY = "moveY";


    [Header("Weapon")]
    public WeaponDataSO weaponData;

}
