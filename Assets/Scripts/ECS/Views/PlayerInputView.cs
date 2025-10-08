using UnityEngine;

public class PlayerInputView : EntityView
{
    private void Start()
    {
        var input = InputManager.Instance;

        input.OnMove += HandleMove;
        input.OnAttackPressed += HandleAttack;

        input.OnSkill1Pressed += () => HandleSkill(1, true);
        input.OnSkill1Released += () => HandleSkill(1, false);

        input.OnSkill2Pressed += () => HandleSkill(2, true);
        input.OnSkill2Released += () => HandleSkill(2, false);

        input.OnSkill3Pressed += () => HandleSkill(3, true);
        input.OnSkill3Released += () => HandleSkill(3, false);
    }

    private void OnDestroy()
    {
        var input = InputManager.Instance;

        input.OnMove -= HandleMove;
        input.OnAttackPressed -= HandleAttack;

        input.OnSkill1Pressed -= () => HandleSkill(1, true);
        input.OnSkill1Released -= () => HandleSkill(1, false);

        input.OnSkill2Pressed -= () => HandleSkill(2, true);
        input.OnSkill2Released -= () => HandleSkill(2, false);

        input.OnSkill3Pressed -= () => HandleSkill(3, true);
        input.OnSkill3Released -= () => HandleSkill(3, false);
    }

    private void HandleSkill(int index, bool isPressed)
    {
        WorldInstance.Events.Publish(new SkillInputEvent(EntityInstance, index, isPressed));
    }

    private void HandleAttack()
    {
        WorldInstance.Events.Publish(new AttackInputEvent(EntityInstance));
    }

    private void HandleMove(Vector2 input)
    {
        WorldInstance.Events.Publish(new MoveInputEvent(EntityInstance, input));
    }
}
