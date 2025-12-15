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
        foreach (var (entity, owner, sync) in _world.Components.Query<NetworkOwnerComponent, NetworkSyncComponent>())
        {
            // Only owner this client allow to control
            if (!owner.IsLocalPlayer)
            {
                continue;
            }

            // Movement
            Vector2 moveInput = _input.GetMoveInput();
            _world.Events.Publish(new MovePressedInputEvent(entity, moveInput));

            // LeftMouse clicked
            if (_input.IsLeftMouseDown() && _world.Components.TryGet(entity, out ActionFlagComponent flags))
            {
                Vector3 mousePos = _input.GetMouseWorldPosition();

                if (flags.Get(ActionFlag.SkillPreview))
                {
                    _world.Events.Publish(new SkillExecutionRequestEvent(entity));
                }
                else
                {
                    // Local client will predict the action
                    _world.Events.Publish(new AttackPressedInputEvent(entity));

                    // Request Server validation
                    sync.SyncView.RequestAttackServerRpc(mousePos);
                }
            }

            // Skills
            for (int i = 1; i <= 3; i++)
            {
                if (_input.IsSkillDown(i))
                {
                    Vector3 mousePos = _input.GetMouseWorldPosition();

                    // Client predict preview immediately
                    _world.Events.Publish(new SkillPressedInputEvent(entity, i, true));

                    // Request server validation
                    sync.SyncView.RequestSkillServerRpc(i, true, mousePos);
                }
                else if (_input.IsSkillUp(i))
                {
                    _world.Events.Publish(new SkillPressedInputEvent(entity, i, false));

                    sync.SyncView.RequestSkillServerRpc(i, false, Vector3.zero);
                }
            }

            _world.Events.Publish(new MouseWorldInputEvent(entity, _input.GetMouseWorldPosition()));
        }
    }

    public void FixedUpdate(float dt) { }

    public void Shutdown() { }
}
