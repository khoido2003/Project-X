using UnityEngine;

public class MouseLookView : EntityView
{
    private const float ROTATION_SPEED = 650f;

    private MouseWorldPosition mouseWorldPosition;

    private void Awake()
    {
        mouseWorldPosition = GetComponent<MouseWorldPosition>();
    }

    private void Start()
    {
        if (mouseWorldPosition != null)
        {
            mouseWorldPosition.OnDirectionToMouseChanged += MouseWorldPosition_OnDirectionToMouseChanged;
        }
    }

    private void OnDestroy()
    {
        if (mouseWorldPosition != null)
        {
            mouseWorldPosition.OnDirectionToMouseChanged -= MouseWorldPosition_OnDirectionToMouseChanged;
        }
    }

    private void MouseWorldPosition_OnDirectionToMouseChanged(Vector3 directionToMouse)
    {
        if (directionToMouse.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(directionToMouse);

            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                ROTATION_SPEED * Time.deltaTime
            );
        }
    }
}
