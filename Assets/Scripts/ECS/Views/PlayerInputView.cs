using UnityEngine;

public class PlayerInputView : EntityView
{
    private IInputService _input;
    private Vector2 _previousMoveInput;

    private void Start()
    {
        _input = WorldInstance.Services.Resolve<IInputService>();

        if (_input == null)
        {
            Debug.LogError("IInputService not found in world!");
        }
    }

    private void Update()
    {
        if (_input == null)
        {
            return;
        }

        ListenMovementInput();
        ListenAttackInput();
        ListenSkillInput();
    }

    private void ListenMovementInput()
    {
        Vector2 move = _input.GetMoveInput();
        if (move != _previousMoveInput)
        {
            WorldInstance.Events.Publish(new MoveInputEvent(EntityInstance, move));
            _previousMoveInput = move;
        }
    }

    private void ListenAttackInput()
    {
        if (_input.IsAttackPressed())
        {
            WorldInstance.Events.Publish(new AttackInputEvent(EntityInstance));
        }
    }

    private void ListenSkillInput()
    {
        for (int i = 1; i <= 3; i++)
        {
            if (_input.IsSkillPressed(i))
            {
                WorldInstance.Events.Publish(new SkillInputEvent(EntityInstance, i, true));
            }
        }
    }
}
