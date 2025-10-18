using UnityEngine;

public interface IInputService
{
    Vector2 GetMoveInput();

    public bool IsLeftMouseDown();
    public bool IsLeftMouseHeld();
    public bool IsLeftMouseUp();

    public bool IsSkillDown(int index);
    public bool IsSkillUp(int index);

    Vector3 GetMouseWorldPosition();
}
