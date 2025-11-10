using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public abstract class NetworkSO : ScriptableObject
{
    [SerializeField]
    public string assetId;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (string.IsNullOrEmpty(assetId))
        {
            string path = AssetDatabase.GetAssetPath(this);
            assetId = AssetDatabase.AssetPathToGUID(path);
            EditorUtility.SetDirty(this);
        }
    }
#endif
}
