using System;
using UnityEngine;
using UnityEngine.UI;

public class HealthBarUI : MonoBehaviour
{
    [SerializeField]
    private Image fillImage;

    [SerializeField]
    private Color greenColor = Color.green;

    [SerializeField]
    private Color yellowColor = Color.yellow;

    [SerializeField]
    private Color redColor = Color.red;

    private Transform mainCameraTransform;

    private World _world;
    private EntityView _entityView;

    private void Awake()
    {
        mainCameraTransform = Camera.main.transform;
        _entityView = GetComponentInParent<EntityView>();
    }

    private void Start()
    {
        _world = WorldRunner.Instance.World;

        _world.Events.Subscribe<HealthChangedEvent>(OnHealthChanged);
    }

    private void LateUpdate()
    {
        LookAtCamera();
    }

    // LEGACY
    public void Bind(HealthComponent healthComponent)
    {
        healthComponent.OnHealthChanged += UpdateHealthBar;
    }

    private void OnHealthChanged(HealthChangedEvent @event)
    {
        if (_entityView == null)
        {
            return;
        }

        if (@event.Entity != _entityView.EntityInstance)
        {
            return;
        }

        UpdateHealthBar(@event.CurrentHealth, @event.MaxHealth);
    }

    private void LookAtCamera()
    {
        Vector3 direction = mainCameraTransform.position - transform.position;
        direction.y = 0f;

        transform.rotation = Quaternion.LookRotation(direction);
    }

    private void UpdateHealthBar(float current, float max)
    {
        float normalized = Mathf.Clamp01(current / max);

        fillImage.fillAmount = normalized;

        if (normalized > 0.6f)
        {
            fillImage.color = greenColor;
        }
        else if (normalized > 0.3f)
        {
            fillImage.color = yellowColor;
        }
        else
        {
            fillImage.color = redColor;
        }
    }
}
