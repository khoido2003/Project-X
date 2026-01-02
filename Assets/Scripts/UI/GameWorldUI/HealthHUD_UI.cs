using TMPro;
using UnityEngine;

public class HealthHUD_UI : MonoBehaviour
{
    [SerializeField]
    private TextMeshProUGUI healthText;

    private World _world;
    private EntityViewRegistry _registry;
    private EntityId _localPlayerEntity;

    public void Bind(World world)
    {
        _world = world;
        _registry = world.Services.Resolve<EntityViewRegistry>();

        _world.Events.Subscribe<HealthChangedEvent>(OnHealthChangedEvent);

        // Find local player's health to display
        FindAndDisplayLocalPlayerHealth();
    }

    private void FindAndDisplayLocalPlayerHealth()
    {
        foreach (var (entity, owner, health) in _world.Components.Query<NetworkOwnerComponent, HealthDataComponent>())
        {
            if (owner.IsLocalPlayer && _world.Components.Has<PlayerTagComponent>(entity))
            {
                _localPlayerEntity = entity;
                healthText.text = Mathf.RoundToInt(health.CurrentHealth).ToString();
                Debug.Log($"[HealthHUD_UI] Found local player entity: {entity.Id}, Health: {health.CurrentHealth}");
                break;
            }
        }
    }

    private void OnDestroy()
    {
        _world?.Events.Unsubscribe<HealthChangedEvent>(OnHealthChangedEvent);
    }

    private void OnHealthChangedEvent(HealthChangedEvent @event)
    {
        // Only update for local player
        if (!@event.Entity.Equals(_localPlayerEntity))
        {
            // If we haven't found local player yet, try again
            if (_localPlayerEntity.Equals(default))
            {
                FindAndDisplayLocalPlayerHealth();
            }
            return;
        }

        healthText.text = Mathf.RoundToInt(@event.CurrentHealth).ToString();
    }
}
