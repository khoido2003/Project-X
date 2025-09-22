using UnityEngine;

[CreateAssetMenu(fileName = "StatsData", menuName = "Character/StatsData")]
public class StatsData : ScriptableObject
{
    public float maxHealth = 100f;
    public float moveSpeed = 5f;
    public float defense = 0f;
}
