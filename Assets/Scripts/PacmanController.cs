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

    Vector2 swipeStart;
    bool swipeTracking;
    const float SwipeThreshold = 30f; // pixels

    protected override void FrameTick()
    {
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.upArrowKey.isPressed || kb.wKey.isPressed) bufferedDirection = Vector2Int.up;
            else if (kb.downArrowKey.isPressed || kb.sKey.isPressed) bufferedDirection = Vector2Int.down;
            else if (kb.leftArrowKey.isPressed || kb.aKey.isPressed) bufferedDirection = Vector2Int.left;
            else if (kb.rightArrowKey.isPressed || kb.dKey.isPressed) bufferedDirection = Vector2Int.right;
        }

        ReadSwipe();

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

    // Swipe anywhere to steer; re-anchors after each registered swipe so you can
    // chain direction changes without lifting your finger.
    void ReadSwipe()
    {
        var ts = Touchscreen.current;
        if (ts == null) return;

        var touch = ts.primaryTouch;
        if (touch.press.wasPressedThisFrame)
        {
            swipeStart = touch.position.ReadValue();
            swipeTracking = true;
            return;
        }

        if (!touch.press.isPressed) { swipeTracking = false; return; }
        if (!swipeTracking) return;

        Vector2 delta = touch.position.ReadValue() - swipeStart;
        if (delta.magnitude < SwipeThreshold) return;

        bufferedDirection = Mathf.Abs(delta.x) > Mathf.Abs(delta.y)
            ? (delta.x > 0 ? Vector2Int.right : Vector2Int.left)
            : (delta.y > 0 ? Vector2Int.up : Vector2Int.down);

        swipeStart = touch.position.ReadValue();
    }

    protected override Vector2Int GetDesiredDirection() => bufferedDirection;
}
