using UnityEngine;

public class CharacterFactory
{
    private readonly World _world;

    public CharacterFactory(World world)
    {
        _world = world;
    }

    public EntityId CreateCharacter(CharacterSO data, Vector3 spawnPos)
    {
        GameObject instance = Object.Instantiate(data.prefab, spawnPos, Quaternion.identity);

        EntityId entity = _world.CreateEntity();

        foreach (EntityView view in instance.GetComponents<EntityView>())
        {
            view.Bind(_world, entity);
        }

        _world.Components.Add(
            entity,
            new MovementData
            {
                MoveSpeed = data.moveSpeed,
                ForwardMultiplier = data.forwardMultiplier,
                IsPlayerControlled = data.isPlayer,
            }
        );

        _world.Components.Add(entity, new AnimationData { });

        instance.name = $"{data.characterName}_Entity{entity.Id}";

        return entity;
    }
}
