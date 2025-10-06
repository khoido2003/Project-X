using UnityEngine;

[CreateAssetMenu(menuName = "Game/Spawn Config", fileName = "NewSpawnConfig")]
public class SpawnConfigSO : ScriptableObject
{
    [Header("Player Spawn Rules")]
    public int maxPlayers = 4;

    [Header("Enemy Spawn Rules")]
    public int maxEnemies = 10;

    [Header("References")]
    public CharacterSO[] possiblePlayers;
}
