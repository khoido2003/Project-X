using TMPro;
using UnityEngine;

public class HealthHUD_UI : MonoBehaviour
{
    [SerializeField]
    private TextMeshProUGUI healthText;

    private World _world;
    private EntityViewRegistry _registry;

    public void Bind(World world)
    {
        _world = world;
        _registry = world.Services.Resolve<EntityViewRegistry>();

        _world.Events.Subscribe<HealthChangedEvent>(OnHealthChangedEvent);

        foreach (var (entity, health) in _world.Components.Query<HealthDataComponent>())
        {
            if (_world.Components.Has<PlayerTagComponent>(entity))
            {
                healthText.text = Mathf.RoundToInt(health.CurrentHealth).ToString();
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
        if (!_world.Components.Has<PlayerTagComponent>(@event.Entity))
        {
            return;
        }

        healthText.text = Mathf.RoundToInt(@event.CurrentHealth).ToString();
    }
}
