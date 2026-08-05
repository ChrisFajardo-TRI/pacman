using UnityEngine;
using UnityEngine.InputSystem;

public class PacmanController : GridMover
{
    Vector2Int bufferedDirection;
    Vector3 baseScale;
    bool baseScaleCaptured;

    public Vector3 BaseScale => baseScale;

    void Start()
    {
        baseScale = transform.localScale;
        baseScaleCaptured = true;
    }

    public void ResetVisual()
    {
        if (baseScaleCaptured) transform.localScale = baseScale;
        transform.rotation = Quaternion.identity;
    }

    protected override void FrameTick()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.upArrowKey.isPressed || kb.wKey.isPressed) bufferedDirection = Vector2Int.up;
        else if (kb.downArrowKey.isPressed || kb.sKey.isPressed) bufferedDirection = Vector2Int.down;
        else if (kb.leftArrowKey.isPressed || kb.aKey.isPressed) bufferedDirection = Vector2Int.left;
        else if (kb.rightArrowKey.isPressed || kb.dKey.isPressed) bufferedDirection = Vector2Int.right;

        if (paused || !baseScaleCaptured) return;

        if (Direction != Vector2Int.zero)
        {
            transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(Direction.y, Direction.x) * Mathf.Rad2Deg);
            // chomp pulse while moving
            float pulse = 1f + 0.08f * Mathf.Sin(Time.time * 18f);
            transform.localScale = baseScale * pulse;
        }
        else
        {
            transform.localScale = baseScale;
        }
    }

    protected override Vector2Int GetDesiredDirection() => bufferedDirection;
}
