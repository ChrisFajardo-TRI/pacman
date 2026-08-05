using UnityEngine;
using UnityEngine.InputSystem;

public class PacmanController : GridMover
{
    Vector2Int bufferedDirection;

    protected override void FrameTick()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.upArrowKey.isPressed || kb.wKey.isPressed) bufferedDirection = Vector2Int.up;
        else if (kb.downArrowKey.isPressed || kb.sKey.isPressed) bufferedDirection = Vector2Int.down;
        else if (kb.leftArrowKey.isPressed || kb.aKey.isPressed) bufferedDirection = Vector2Int.left;
        else if (kb.rightArrowKey.isPressed || kb.dKey.isPressed) bufferedDirection = Vector2Int.right;

        if (Direction.x != 0 || Direction.y != 0)
            transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(Direction.y, Direction.x) * Mathf.Rad2Deg);
    }

    protected override Vector2Int GetDesiredDirection() => bufferedDirection;
}
