using Unity.Netcode;
using UnityEngine;

public class InputSystem : ISystem
{
    private World _world;
    private IInputService _input;

    public void Initialize(World world)
    {
        _world = world;
        _input = world.Services.Resolve<IInputService>();
        Debug.Log("[InputSystem] Initialized on client");
    }

    private bool _hasLoggedQuery = false;

    public void Update(float dt)
    {
        if (!_hasLoggedQuery)
        {
            var count = 0;
            foreach (
                var (entity, owner, sync) in _world.Components.Query<NetworkOwnerComponent, NetworkSyncComponent>()
            )
            {
                count++;
                Debug.Log($"[InputSystem] Found entity {entity.Id}, IsLocalPlayer: {owner.IsLocalPlayer}");
            }
            _hasLoggedQuery = true;
        }

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
                Debug.Log($"[InputSystem] Left mouse clicked for entity {entity.Id} at {mousePos}");

                if (flags.Get(ActionFlag.SkillPreview))
                {
                    Debug.Log($"[InputSystem] Skill preview active, executing skill");
                    _world.Events.Publish(new SkillExecutionRequestEvent(entity));
                }
                else
                {
                    // For HOST player, set attack direction BEFORE publishing attack event
                    // This ensures the direction is available when animation fires HandleAttackHit
                    // For remote clients, the ServerRpc will set the direction on the server
                    if (NetworkManager.Singleton.IsServer && owner.IsLocalPlayer)
                    {
                        if (
                            _world.Components.TryGet(entity, out AttackDataComponent attack)
                            && _world.Components.TryGet(entity, out WeaponDataComponent weapon)
                        )
                        {
                            // Don't overwrite direction if still in cooldown
                            // This prevents subsequent clicks from changing direction mid-animation
                            // Check cooldown instead of IsAttacking since IsAttacking is set later in the frame
                            float timeSinceAttack = Time.time - attack.LastAttackTime;
                            if (timeSinceAttack < weapon.BaseCooldown)
                            {
                                Debug.Log(
                                    $"[InputSystem] Attack blocked - still in cooldown ({timeSinceAttack:F3}s / {weapon.BaseCooldown}s)"
                                );
                                continue; // Skip entire attack processing
                            }

                            Vector3 attackDir = mousePos;
                            if (_world.Components.TryGet(entity, out TransformComponent transform))
                            {
                                attackDir = mousePos - transform.Position;
                            }
                            else
                            {
                                var registry = _world.Services.Resolve<EntityViewRegistry>();
                                if (registry.TryGet(entity, out EntityView view))
                                {
                                    attackDir = mousePos - view.transform.position;
                                }
                            }

                            attackDir.y = 0f;
                            if (attackDir.sqrMagnitude < 0.0001f)
                            {
                                var registry = _world.Services.Resolve<EntityViewRegistry>();
                                if (registry.TryGet(entity, out EntityView view))
                                {
                                    attackDir = view.transform.forward;
                                }
                                else
                                {
                                    attackDir = Vector3.forward;
                                }
                            }
                            attack.AttackDirection = attackDir.normalized;
                            Debug.Log($"[InputSystem] Host set attack direction: {attack.AttackDirection}");
                        }
                    }

                    Debug.Log($"[InputSystem] Requesting attack RPC to server");

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
