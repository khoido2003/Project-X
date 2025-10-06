using UnityEngine;

public enum SpawnType
{
    Player,
    Enemy,
    Boss,
}

public class SpawnPoint : MonoBehaviour
{
    [Header("Spawn Info")]
    public SpawnType type;

    public int index;

    private void OnDrawGizmos()
    {
        Gizmos.color = type switch
        {
            SpawnType.Player => Color.green,
            SpawnType.Enemy => Color.orange,
            SpawnType.Boss => Color.red,

            _ => Color.white,
        };

        Gizmos.DrawSphere(transform.position, 0.3f);
    }
}
