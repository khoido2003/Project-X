using UnityEngine;

public class LookAtMouseView : EntityView
{
    private const float ROTATION_SPEED = 650f;

    private IInputService _inputService;
    private Transform _transform;

    private bool _isLocalPlayer;

    private void Start()
    {
        _transform = transform;
        TryResolveInput();
        CheckIfLocalPlayer();
    }

    private void Update()
    {
        if (!_isLocalPlayer)
        {
            return;
        }

        if (_inputService == null)
        {
            TryResolveInput();

            if (_inputService == null)
            {
                return;
            }
        }

        Vector3 aimDir = (_inputService.GetMouseWorldPosition() - transform.position).normalized;

        aimDir.y = 0;

        if (aimDir.sqrMagnitude < 0.1f)
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

    private void CheckIfLocalPlayer()
    {
        if (WorldInstance != null && WorldInstance.Components.TryGet(EntityInstance, out NetworkOwnerComponent owner))
        {
            _isLocalPlayer = owner.IsLocalPlayer;
        }
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
