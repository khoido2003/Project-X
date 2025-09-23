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

    private void Start()
    {
        mainCameraTransform = Camera.main.transform;
    }

    private void LateUpdate()
    {
        LookAtCamera();
    }

    public void Bind(HealthComponent healthComponent)
    {
        healthComponent.OnHealthChanged += UpdateHealth;
    }

    private void LookAtCamera()
    {
        Vector3 direction = mainCameraTransform.position - transform.position;
        direction.y = 0f;

        transform.rotation = Quaternion.LookRotation(direction);
    }

    private void UpdateHealth(float current, float max)
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
