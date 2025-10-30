using UnityEngine;

public interface IObjectPoolService
{
    public GameObject Get(GameObject prefab, Vector3 position, Quaternion rotation);

    public void Return(GameObject prefab, GameObject instance);

    public void ClearAll();
}
