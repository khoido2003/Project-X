using UnityEngine;

public class WeaponSpawnSystem : ISystem
{
    private World _world;

    public void Initialize(World world)
    {
        _world = world;
    }

    public void Update(float dt)
    {
        foreach (var (entity, weapon) in _world.Components.Query<WeaponDataComponent>())
        {
            if (weapon.WeaponInstance != null)
            {
                continue;
            }

            if (weapon.WeaponData == null || weapon.WeaponData.weaponPrefab == null)
            {
                Debug.LogError($"Entity {entity} has no valid WeaponData");
                continue;
            }

            Transform holder = weapon.WeaponHolder;

            if (holder == null)
            {
                Debug.LogError("Missing WeaponHolder!");
            }

            GameObject instance = Object.Instantiate(weapon.WeaponData.weaponPrefab, holder);
            instance.transform.localPosition = weapon.WeaponData.spawnPositionOffset;
            instance.transform.localRotation = Quaternion.Euler(weapon.WeaponData.spawnRotationOffset);

            weapon.WeaponInstance = instance;
        }
    }

    public void FixedUpdate(float dt) { }

    public void Shutdown() { }
}
