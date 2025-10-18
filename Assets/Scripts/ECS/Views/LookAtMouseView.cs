using UnityEngine;

public class LookAtMouseView : EntityView
{
    private const float ROTATION_SPEED = 650f;

    private IInputService _inputService;
    private Transform _transform;

    private void Start()
    {
        _transform = transform;
        TryResolveInput();
    }

    private void Update()
    {
        if (_inputService == null)
        {
            TryResolveInput();
            if (_inputService == null)
                return;
        }

        Vector3 aimDir = (_inputService.GetMouseWorldPosition() - transform.position).normalized;

        aimDir.y = 0;

        if (aimDir.sqrMagnitude < 0.01f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(aimDir);
        _transform.rotation = Quaternion.RotateTowards(
            _transform.rotation,
            targetRotation,
            ROTATION_SPEED * Time.deltaTime
        );
    }

    private void TryResolveInput()
    {
        if (WorldInstance == null)
        {
            return;
        }

        _inputService = WorldInstance.Services.Resolve<IInputService>();
    }
}
