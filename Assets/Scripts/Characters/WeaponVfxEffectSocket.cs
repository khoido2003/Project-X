using UnityEngine;

public enum WeaponVfxEffectSocketName
{
    SLASH,
    CHARGE,
    IMPACT,
}

public class WeaponVfxEffectSocket : MonoBehaviour
{
    [Header("Effect Spawn Points")]
    [SerializeField]
    private Transform slashPoint;

    [SerializeField]
    private Transform chargePoint;

    [SerializeField]
    private Transform impactPoint;

    public Transform GetSocket(WeaponVfxEffectSocketName socketName)
    {
        return socketName switch
        {
            WeaponVfxEffectSocketName.SLASH => slashPoint,
            WeaponVfxEffectSocketName.CHARGE => chargePoint,
            WeaponVfxEffectSocketName.IMPACT => impactPoint,
        };
    }
}
