using UnityEngine;

public class AnimationDataComponent
{
    [Header("Animation Settings")]
    public string IsMovingParam;
    public string MoveXParam;
    public string MoveYParam;

    [Header("Runtime State")]
    public bool IsMoving;
    public float MoveX;
    public float MoveY;
}
