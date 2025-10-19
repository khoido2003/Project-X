using UnityEngine;

public interface IAnimationRelayReceiver
{
    public void OnAnimationEvent(AnimationEventRelayName eventName);
}
