using UnityEngine;

public interface IInputService
{
    Vector2 GetMoveInput();
    bool IsAttackPressed();
    bool IsSkillPressed(int index);
    Vector3 GetMouseWorldPosition();
    Vector3 GetAimDirection(Vector3 origin);
}
