// using UnityEngine;
//
// public class WeaponSpawnSystem : ISystem
// {
//     private World _world;
//
//     public void Initialize(World world)
//     {
//         _world = world;
//         _world.Components.OnComponentAdded += HandleComponentAdded;
//
//         foreach (var (entity, weapon) in _world.Components.Query<WeaponDataComponent>())
//         {
//             TrySpawnWeapon(entity, weapon);
//         }
//     }
//
//     private void HandleComponentAdded(EntityId entity, System.Type type)
//     {
//         if (type != typeof(WeaponDataComponent))
//             return;
//
//         if (_world.Components.TryGet(entity, out WeaponDataComponent weapon))
//         {
//             TrySpawnWeapon(entity, weapon);
//         }
//     }
//
//     private void TrySpawnWeapon(EntityId entity, WeaponDataComponent weapon)
//     {
//         if (weapon.WeaponInstance != null)
//             return;
//
//         if (weapon.WeaponPrefab == null)
//         {
//             Debug.LogWarning($"[WeaponSpawnSystem] Entity {entity.Id} has no weapon prefab!");
//             return;
//         }
//
//         if (weapon.WeaponHolder == null)
//         {
//             Debug.LogWarning($"[WeaponSpawnSystem] Entity {entity.Id} has no weapon holder!");
//             return;
//         }
//
//         GameObject instance = Object.Instantiate(weapon.WeaponPrefab, weapon.WeaponHolder);
//         instance.transform.localPosition = weapon.SpawnPositionOffset;
//         instance.transform.localRotation = Quaternion.Euler(weapon.SpawnRotationOffset);
//
//         weapon.WeaponInstance = instance;
//     }
//
//     public void Update(float dt) { }
//
//     public void FixedUpdate(float dt) { }
//
//     public void Shutdown()
//     {
//         _world.Components.OnComponentAdded -= HandleComponentAdded;
//     }
// }
