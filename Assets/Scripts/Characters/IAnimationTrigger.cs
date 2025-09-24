using System;
using UnityEngine;

public interface IAnimationTrigger
{
    public event Action<string> OnTriggerAnimation;
    public event Action<string, float> OnSetFloatParameter;
    public event Action<string, bool> OnSetBoolParameter;
}
