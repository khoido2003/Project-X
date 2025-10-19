using UnityEngine;

public enum AnimationEventRelayName
{
    ATTACK_HIT,
    SKILL_HIT,
}

public class AnimationEventRelay : MonoBehaviour
{
    private IAnimationRelayReceiver[] receivers;

    private void Start()
    {
        receivers = GetComponentsInParent<IAnimationRelayReceiver>();
    }

    public void OnAnimationTrigger(int eventId)
    {
        AnimationEventRelayName eventName = (AnimationEventRelayName)eventId;

        foreach (var r in receivers)
        {
            r.OnAnimationEvent(eventName);
        }
    }
}
