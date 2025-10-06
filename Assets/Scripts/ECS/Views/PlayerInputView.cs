using UnityEngine;

public class PlayerInputView : EntityView
{
    private void Start()
    {
        InputManager.Instance.OnMove += InputManager_OnMove;
    }

    private void OnDestroy()
    {
        InputManager.Instance.OnMove -= InputManager_OnMove;
    }

    private void InputManager_OnMove(Vector2 input)
    {
        EntityId entity = EntityInstance;

        if (WorldInstance.Components.TryGet(entity, out MovementData movement))
        {
            movement.InputDirection = input;
        }
    }
}
