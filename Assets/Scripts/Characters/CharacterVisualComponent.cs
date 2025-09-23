using UnityEngine;

public class CharacterVisualComponent : MonoBehaviour
{
    private GameObject visualInstance;
    private Transform visualHolder;

    public void Initialize(CharacterData characterData, Transform holder = null)
    {
        if (characterData == null || characterData.characterVisualPrefab == null)
        {
            Debug.LogError("Character Visual Prefab is missing!");
            return;
        }

        visualHolder = holder ?? transform;

        visualInstance = Instantiate(characterData.characterVisualPrefab, visualHolder);

        visualInstance.transform.localPosition = characterData.characterVisualPositionOffset;

        visualInstance.transform.localRotation = Quaternion.Euler(
            characterData.characterVisualRotationOffset
        );
    }

    public GameObject GetVisualInstance()
    {
        return visualInstance;
    }

    private void OnDestroy()
    {
        if (visualInstance != null)
        {
            Destroy(visualInstance);
        }
    }
}
