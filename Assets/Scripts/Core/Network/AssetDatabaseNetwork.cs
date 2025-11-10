using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class AssetDatabaseNetwork
{
    private static Dictionary<string, NetworkSO> _allAssets = new();
    private static Dictionary<Type, Dictionary<string, NetworkSO>> _typeCache = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Initialize()
    {
        LoadAllAssets();
        Debug.Log($"AssetDatabaseNetwork: {_allAssets.Count} assets loaded");
    }

    static void LoadAllAssets()
    {
        _allAssets.Clear();
        _typeCache.Clear();

        var allAssets = Resources.LoadAll<NetworkSO>("");
        if (allAssets == null)
        {
            Debug.LogWarning("AssetDatabaseNetwork: no NetworkSO assets found");
            return;
        }

        foreach (var asset in allAssets)
        {
            if (asset == null)
            {
                continue;
            }
            if (string.IsNullOrEmpty(asset.assetId))
            {
                asset.assetId = Guid.NewGuid().ToString();
            }

            _allAssets[asset.assetId] = asset;

            var type = asset.GetType();
            if (!_typeCache.TryGetValue(type, out var typeDict))
            {
                typeDict = new Dictionary<string, NetworkSO>();
                _typeCache[type] = typeDict;
            }
            typeDict[asset.assetId] = asset;
        }
    }

    public static T GetAsset<T>(string assetId)
        where T : NetworkSO
    {
        if (string.IsNullOrEmpty(assetId))
        {
            return null;
        }
        if (_typeCache.TryGetValue(typeof(T), out var typeDict) && typeDict.TryGetValue(assetId, out var asset))
        {
            return asset as T;
        }
        return null;
    }

    public static T[] GetAllAssets<T>()
        where T : NetworkSO
    {
        if (_typeCache.TryGetValue(typeof(T), out var typeDict))
            return typeDict.Values.Cast<T>().ToArray();
        return Array.Empty<T>();
    }

    public static T GetAssetByName<T>(string name)
        where T : NetworkSO
    {
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }
        return GetAllAssets<T>().FirstOrDefault(x => x.name == name);
    }
}
