using UnityEngine;

public enum SkillVfxEffectSocketName
{
    CHARGE,
    IMPACT,
    SHIELD,
}

public class SkillVfxEffectSocket : MonoBehaviour
{
    [Header("Effect Spawn Points")]
    [SerializeField]
    private Transform chargePoint;

    [SerializeField]
    private Transform impactPoint;

    [SerializeField]
    private Transform shieldPoint;

    public Transform GetSocket(SkillVfxEffectSocketName socketName)
    {
        return socketName switch
        {
            SkillVfxEffectSocketName.CHARGE => chargePoint,
            SkillVfxEffectSocketName.IMPACT => impactPoint,
            SkillVfxEffectSocketName.SHIELD => shieldPoint,
        };
    }
}
