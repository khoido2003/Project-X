using System;
using UnityEngine;

[DefaultExecutionOrder(-110)]
public class MouseWorldPosition : MonoBehaviour
{
    public static MouseWorldPosition Instance { get; private set; }

    public event Action<Vector3> OnDirectionToMouseChanged;
    private Vector3 directionToMouse = Vector3.forward;

    [SerializeField]
    private LayerMask mouseLayerMask;

    private void Awake()
    {
        if (Instance != null)
        {
            Debug.LogError("MouseWorldPosition Instance already exists!");
            return;
        }

        Instance = this;
    }

    private void Update()
    {
        UpdateMouseDirection();
    }

    private void UpdateMouseDirection()
    {
        Vector3 worldPos = GetWorldPosition();
        Vector3 newDirection = (worldPos - transform.position).normalized;
        newDirection.y = 0f;

        if (newDirection.sqrMagnitude > 0.01f && newDirection != directionToMouse)
        {
            directionToMouse = newDirection;
            OnDirectionToMouseChanged?.Invoke(directionToMouse);
        }
    }

    public Vector3 GetDirectionToMouse()
    {
        return directionToMouse;
    }

    public Vector3 GetWorldPosition()
    {
        Ray ray = Camera.main.ScreenPointToRay(InputManager.Instance.GetMouseScreeenPosition());

        Physics.Raycast(ray, out RaycastHit raycastHit, float.MaxValue, mouseLayerMask);

        return raycastHit.point;
    }

    public Vector3 GetWorldPositionClamped(float maxRange)
    {
        Vector3 worldPos = GetWorldPosition();
        float distance = Vector3.Distance(transform.position, worldPos);

        Vector3 direction = (worldPos - transform.position).normalized;

        direction.y = 0f;

        return transform.position + direction * Mathf.Min(distance, maxRange);
    }
}
