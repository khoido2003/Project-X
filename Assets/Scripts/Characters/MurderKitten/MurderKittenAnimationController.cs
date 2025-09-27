using UnityEngine;

public class MurderKittenAnimationController : AnimationControllerComponent
{
    protected override void OnTriggerAnimation(string triggerName)
    {
        SwitchAnimationLayer(1);
        Debug.Log("Switch");
        base.OnTriggerAnimation(triggerName);
    }
}
