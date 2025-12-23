using UnityEngine;

public abstract class SkillExecutorView : EntityView
{
    public abstract SkillCategory Category { get; }

    protected virtual void Start()
    {
        Debug.Log($"[SkillExecutorView] {GetType().Name} Start() called, Category: {Category}, " +
                  $"WorldInstance: {WorldInstance != null}, EntityInstance: {EntityInstance.Id}, " +
                  $"IsServer: {Unity.Netcode.NetworkManager.Singleton?.IsServer}");
        
        if (WorldInstance == null)
        {
            Debug.LogError($"[SkillExecutorView] {GetType().Name} Start() called but WorldInstance is null! Events will NOT be subscribed.");
            return;
        }
        
        WorldInstance.Events.Subscribe<SkillConfirmExecutionEvent>(OnSkillConfirmExecutionEvent);
        WorldInstance.Events.Subscribe<SkillEffectTriggerEvent>(OnSkillEffectTriggerEvent);
        
        Debug.Log($"[SkillExecutorView] {GetType().Name} subscribed to SkillConfirmExecutionEvent and SkillEffectTriggerEvent");
    }

    private void OnSkillEffectTriggerEvent(SkillEffectTriggerEvent @event)
    {
        if (@event.Skill == null || @event.Skill.category != Category)
        {
            return;
        }

        if (@event.Caster != EntityInstance)
        {
            return;
        }
        
        Debug.Log($"[SkillExecutorView] {GetType().Name} received SkillEffectTriggerEvent for {Category}, Caster: {@event.Caster.Id}, IsServer: {Unity.Netcode.NetworkManager.Singleton.IsServer}");

        // Play skill sound - simplified!
        if (@event.Skill.activateSound != null)
        {
            var registry = WorldInstance.Services.Resolve<EntityViewRegistry>();
            if (registry.TryGet(EntityInstance, out EntityView view))
            {
                // Category automatically determined by system
                var category = WorldInstance.Components.Has<PlayerTagComponent>(EntityInstance)
                    ? AudioCategory.Player
                    : AudioCategory.Enemy;

                AudioHelper.PlaySound3D(WorldInstance, @event.Skill.activateSound, category, view.transform.position);
            }
        }
        else
        {
            // Profile-driven fallback
            WorldInstance.Events.Publish(new AudioCueEvent(EntityInstance, SoundType.Skill, @event.TargetPoint));
        }

        // Animation
        WorldInstance.Events.Publish(
            new AnimationParameterEvent(
                EntityInstance,
                @event.Skill.activationAnimationTrigger,
                AnimationParameterType.Trigger,
                null
            )
        );

        // Buffer skill for execution
        var skillBuffer = WorldInstance.Components.Get<SkillCastBufferComponent>(EntityInstance);
        skillBuffer.Skill = @event.Skill;
        skillBuffer.Direction = @event.Direction;
        skillBuffer.TargetPoint = @event.TargetPoint;


        // CLIENT-SIDE: Spawn visual effects (projectiles with 0 damage)
        // Server spawns networked projectiles via ExecuteSkill, clients spawn local visuals here
        if (!Unity.Netcode.NetworkManager.Singleton.IsServer)
        {
            SpawnClientVisualEffect(@event);
            
            // Reset combat state on client to prevent getting stuck
            WorldInstance.Events.Publish(
                new ExitCombatStateEvent { Entity = @event.Caster, TargetState = CombatState.CastingSkill }
            );
        }
    }

    /// <summary>
    /// Override in subclasses to spawn visual-only projectiles on clients.
    /// Called on clients when SkillEffectTriggerEvent is received.
    /// Projectiles should be spawned with 0 damage.
    /// </summary>
    protected virtual void SpawnClientVisualEffect(SkillEffectTriggerEvent @event)
    {
        // Base implementation does nothing - override in skill executors that have projectiles
    }

    protected virtual void OnSkillConfirmExecutionEvent(SkillConfirmExecutionEvent @event)
    {
        // Only execute if this event is for this entity
        if (@event.Caster != EntityInstance)
        {
            return;
        }

        ExecuteSkill(@event);
    }

    protected virtual void ExecuteSkill(SkillConfirmExecutionEvent @event)
    {
        WorldInstance.Events.Publish(
            new ExitCombatStateEvent { Entity = @event.Caster, TargetState = CombatState.CastingSkill }
        );
    }

    protected void FinishSkill(SkillDefinitionSO skill)
    {
        WorldInstance.Events.Publish(new SkillExecutionFinishedEvent { Caster = EntityInstance, Skill = skill });
    }

    protected virtual void OnDestroy()
    {
        if (WorldInstance == null) return;
        
        StopAllCoroutines(); // Stop any running skill coroutines
        WorldInstance.Events.Unsubscribe<SkillConfirmExecutionEvent>(OnSkillConfirmExecutionEvent);
        WorldInstance.Events.Unsubscribe<SkillEffectTriggerEvent>(OnSkillEffectTriggerEvent);
    }
}
