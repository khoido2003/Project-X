using UnityEngine;

[CreateAssetMenu(fileName = "NewBuff", menuName = "Game/Buff")]
public class BuffSO : ScriptableObject
{
    public enum BuffType
    {
        Health,
        Speed
    }

    [Header("Buff Settings")]
    public BuffType Type;
    public float Value; // Percentage (e.g., 20 for 20%)
    public float Duration; // 0 for instant
    public float RespawnTime = 10f;

    [Header("Visuals")]
    public GameObject Prefab;
}
