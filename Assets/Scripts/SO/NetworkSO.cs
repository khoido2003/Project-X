using System;
using UnityEngine;

public class NetworkSO : ScriptableObject
{
    [HideInInspector]
    public string assetId;

    protected virtual void OnValidate()
    {
        if (string.IsNullOrEmpty(assetId))
        {
            assetId = Guid.NewGuid().ToString();
        }
    }
}
