using UnityEngine;

public class WeaponComponent : MonoBehaviour
{
    private GameObject weaponInstance;
    private Transform weaponHolder;

    public void Initialize(WeaponData weaponData, Transform holder = null)
    {
        if (weaponData == null || weaponData.weaponPrefab == null)
        {
            Debug.LogError("Weapon Data is missing!");
            return;
        }

        // use current root if holder is null
        weaponHolder = holder ?? transform;

        weaponInstance = Instantiate(weaponData.weaponPrefab, weaponHolder);

        weaponInstance.transform.localPosition = weaponData.spawnPositionOffset;
        weaponInstance.transform.localRotation = Quaternion.Euler(weaponData.spawnRotationOffset);
    }

    public GameObject GetWeaponInstance()
    {
        return weaponInstance;
    }

    private void OnDestroy()
    {
        if (weaponInstance != null)
        {
            Destroy(weaponInstance);
        }
    }
}
