using System;
using System.Collections.Generic;
using UnityEngine;

public class AnimationView : EntityView
{
    private Animator animator;
    private bool _isInitialized;
    
    // OPTIMIZATION: Cache parameter names and hashes to avoid string operations every frame
    private HashSet<string> _boolParams;
    private HashSet<string> _floatParams;
    private HashSet<string> _intParams;
    private HashSet<string> _triggerParams;
    private Dictionary<string, int> _paramHashes;

    private void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        CacheAnimatorParameters();
    }
    
    private void CacheAnimatorParameters()
    {
        _boolParams = new HashSet<string>();
        _floatParams = new HashSet<string>();
        _intParams = new HashSet<string>();
        _triggerParams = new HashSet<string>();
        _paramHashes = new Dictionary<string, int>();
        
        if (animator == null)
            return;
            
        foreach (var param in animator.parameters)
        {
            _paramHashes[param.name] = param.nameHash;
            
            switch (param.type)
            {
                case AnimatorControllerParameterType.Bool:
                    _boolParams.Add(param.name);
                    break;
                case AnimatorControllerParameterType.Float:
                    _floatParams.Add(param.name);
                    break;
                case AnimatorControllerParameterType.Int:
                    _intParams.Add(param.name);
                    break;
                case AnimatorControllerParameterType.Trigger:
                    _triggerParams.Add(param.name);
                    break;
            }
        }
    }

    public override void Bind(World world, EntityId entity)
    {
        base.Bind(world, entity);

        WorldInstance.Events.Subscribe<AnimationParameterEvent>(OnAnimationParameter);
        _isInitialized = true;
    }

    private void OnAnimationParameter(AnimationParameterEvent @event)
    {
        // Early exits for performance
        if (!_isInitialized || animator == null)
            return;
            
        if (@event.Entity != EntityInstance)
            return;

        string paramName = @event.ParameterName;
        
        // O(1) parameter existence check via HashSet
        switch (@event.ParameterType)
        {
            case AnimationParameterType.Trigger:
                if (_triggerParams.Contains(paramName))
                    animator.SetTrigger(_paramHashes[paramName]);
                break;
            case AnimationParameterType.Bool:
                if (_boolParams.Contains(paramName))
                    animator.SetBool(_paramHashes[paramName], Convert.ToBoolean(@event.Value));
                break;
            case AnimationParameterType.Float:
                if (_floatParams.Contains(paramName))
                    animator.SetFloat(_paramHashes[paramName], Convert.ToSingle(@event.Value));
                break;
            case AnimationParameterType.Int:
                if (_intParams.Contains(paramName))
                    animator.SetInteger(_paramHashes[paramName], Convert.ToInt32(@event.Value));
                break;
        }
    }

    private void OnDestroy()
    {
        if (_isInitialized && WorldInstance != null)
            WorldInstance.Events.Unsubscribe<AnimationParameterEvent>(OnAnimationParameter);
    }
}

