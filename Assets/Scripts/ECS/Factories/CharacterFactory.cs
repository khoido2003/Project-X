using UnityEngine;

public class CharacterFactory
{
    private readonly World _world;

    public CharacterFactory(World world)
    {
        _world = world;
    }

    public GameObject CreateCharacter(CharacterSO data, Vector3 spawnPos)
    {
        GameObject instance = Object.Instantiate(data.prefab, spawnPos, Quaternion.identity);
        EntityId entity = _world.CreateEntity();

        foreach (EntityView view in instance.GetComponents<EntityView>())
        {
            view.Bind(_world, entity);
        }

        // Movement
        _world.Components.Add(
            entity,
            new MovementDataComponent
            {
                MoveSpeed = data.moveSpeed,
                ForwardMultiplier = data.forwardMultiplier,
                IsPlayerControlled = data.isPlayer,
            }
        );

        // Animation
        _world.Components.Add(entity, new AnimationDataComponent());

        // Attack
        _world.Components.Add(
            entity,
            new AttackDataComponent
            {
                IsPlayerControlled = data.isPlayer,
                LastAttackTime = 0f,
                IsAttacking = false,
            }
        );

        // Weapon
        var weaponObj = Object.Instantiate(data.weaponData.weaponPrefab, instance.transform);
        weaponObj.transform.localPosition = data.weaponData.spawnPositionOffset;
        weaponObj.transform.localRotation = Quaternion.Euler(data.weaponData.spawnRotationOffset);

        _world.Components.Add(
            entity,
            new WeaponDataComponent
            {
                WeaponData = data.weaponData,
                WeaponInstance = weaponObj,
                WeaponHolder = instance.transform,
            }
        );

        instance.name = $"{data.characterName}_Entity{entity.Id}";
        return instance;
    }
}
