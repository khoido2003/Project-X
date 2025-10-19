using UnityEngine;

[CreateAssetMenu(fileName = "CharacterData", menuName = "Character/CharacterData")]
public class CharacterData : ScriptableObject
{
    public string characterName;

    public GameObject characterVisualPrefab;
    public Vector3 characterVisualPositionOffset = Vector3.zero;
    public Vector3 characterVisualRotationOffset = Vector3.zero;

    // Z axis: 1 for Z+, -1 for Z-
    public float forwardDirectionMultiplier = 1f;

    public StatsData stats;
    public WeaponDataSO weapon;
    public SkillData[] skills = new SkillData[3];

    public AudioClip moveSound;
    public AudioClip hurtSound;
    public AudioClip deathSound;
}
