using UnityEngine;

public class MurderKittenAnimationController : AnimationControllerComponent, IAnimationRelayReceiver
{
    protected override void OnTriggerAnimation(string triggerName)
    {
        SwitchAnimationLayer(1);
        Debug.Log("Switch");
        base.OnTriggerAnimation(triggerName);
    }

    public void OnAnimationEvent(AnimationEventRelayName eventName)
    {
        SwitchAnimationLayer(0);
    }
}
