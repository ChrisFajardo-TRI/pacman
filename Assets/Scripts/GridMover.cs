using UnityEngine;

public abstract class GridMover : MonoBehaviour
{
    public float speed = 8f;
    public bool paused;

    public Vector2Int Cell { get; private set; }
    public Vector2Int Direction { get; private set; } = Vector2Int.zero;

    const float ArriveThreshold = 0.02f;

    // Set by FrameTick when the mover positions itself (e.g. ghost bouncing in house).
    protected bool overrideMovement;

    protected virtual void Awake() { }
    protected virtual void FrameTick() { }
    protected virtual Vector2Int GetDesiredDirection() => Direction;
    protected virtual bool Blocked(Vector2Int cell) => GameManager.Instance.IsWall(cell);
    protected virtual float CurrentSpeed() => speed;

    public void WarpTo(Vector2Int cell)
    {
        Cell = cell;
        Direction = Vector2Int.zero;
        transform.position = GameManager.Instance.GridToWorld(cell);
    }

    void Update()
    {
        FrameTick();
        if (paused || overrideMovement) return;

        Vector2 targetPos = GameManager.Instance.GridToWorld(Cell);

        if (Vector2.Distance(transform.position, targetPos) <= ArriveThreshold)
        {
            transform.position = targetPos;

            Vector2Int desired = GetDesiredDirection();
            if (desired != Vector2Int.zero && !Blocked(Cell + desired))
                Direction = desired;
            else if (Direction != Vector2Int.zero && Blocked(Cell + Direction))
                Direction = Vector2Int.zero;

            if (Direction != Vector2Int.zero)
                Cell = GameManager.Instance.WrapCell(Cell + Direction);

            targetPos = GameManager.Instance.GridToWorld(Cell);

            // tunnel wrap: snap to the far side instead of sliding across the maze
            if (Vector2.Distance(transform.position, targetPos) > 2f)
                transform.position = targetPos - (Vector2)Direction;
        }

        transform.position = Vector2.MoveTowards(transform.position, targetPos, CurrentSpeed() * Time.deltaTime);
    }
}
