using UnityEngine;

public class WeaponComponent : MonoBehaviour
{
    private GameObject weaponInstance;
    private Transform weaponHolder;
    private WeaponVfxEffectSocket vfxEffectSocket;

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

        vfxEffectSocket = weaponInstance.GetComponentInChildren<WeaponVfxEffectSocket>();

        if (vfxEffectSocket == null)
        {
            Debug.LogError($"Weapon {weaponData.weaponName} has no WeaponVfxEffectSocket attached.");
        }
    }

    public GameObject GetWeaponInstance()
    {
        return weaponInstance;
    }

    public Transform GetSocket(WeaponVfxEffectSocketName socketName)
    {
        return vfxEffectSocket?.GetSocket(socketName) ?? weaponInstance?.transform;
    }

    private void OnDestroy()
    {
        if (weaponInstance != null)
        {
            Destroy(weaponInstance);
        }
    }
}
