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
            _world.Events.Publish(new MovePressedInputEvent(entity, _input.GetMoveInput()));

            // LeftMouse clicked
            if (_input.IsLeftMouseDown() && _world.Components.TryGet(entity, out ActionFlagComponent flags))
            {
                if (flags.Get(ActionFlag.SkillPreview))
                {
                    _world.Events.Publish(new SkillExecutionRequestEvent(entity));
                }
                else
                {
                    _world.Events.Publish(new AttackPressedInputEvent(entity));
                }
            }

            // Skills
            for (int i = 1; i <= 3; i++)
            {
                if (_input.IsSkillDown(i))
                {
                    _world.Events.Publish(new SkillPressedInputEvent(entity, i, true));
                }
                else if (_input.IsSkillUp(i))
                {
                    _world.Events.Publish(new SkillPressedInputEvent(entity, i, false));
                }
            }

            _world.Events.Publish(new MouseWorldInputEvent(entity, _input.GetMouseWorldPosition()));
        }
    }

    public void FixedUpdate(float dt) { }

    public void Shutdown() { }
}
