using UnityEngine;

public class InputSystem : ISystem
{
    private World _world;
    private IInputService _input;

    public void Initialize(World world)
    {
        _world = world;
        _input = world.Services.Resolve<IInputService>();
    }

    public void Update(float dt)
    {
        foreach (var (entity, _) in _world.Components.Query<PlayerTagComponent>())
        {
            // Movement
            _world.Events.Publish(new MoveInputEvent(entity, _input.GetMoveInput()));

            // Attack
            if (_input.IsAttackPressed() && !SkillPreviewView.IsPreviewActive)
            {
                _world.Events.Publish(new AttackInputEvent(entity));
            }

            // Skills
            for (int i = 1; i <= 3; i++)
            {
                bool pressed = _input.IsSkillPressed(i);
                _world.Events.Publish(new SkillInputEvent(entity, i, pressed));
            }
        }
    }

    public void FixedUpdate(float dt) { }

    public void Shutdown() { }
}
