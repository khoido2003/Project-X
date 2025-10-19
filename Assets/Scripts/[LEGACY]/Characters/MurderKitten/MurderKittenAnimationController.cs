using UnityEngine;

public class MurderKittenAnimationController : AnimationControllerComponent, IAnimationRelayReceiver
{
    public override void OnTriggerAnimation(string triggerName)
    {
        SwitchAnimationLayer(1);
        base.OnTriggerAnimation(triggerName);
    }

    public void OnAnimationEvent(AnimationEventRelayName eventName)
    {
        SwitchAnimationLayer(0);
    }
}
