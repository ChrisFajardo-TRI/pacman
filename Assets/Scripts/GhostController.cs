using System.Collections.Generic;
using UnityEngine;

public class GhostController : GridMover
{
    public enum State { Chase, Frightened, Eaten }

    public State CurrentState { get; private set; } = State.Chase;
    public Vector2Int homeCell;
    public Color baseColor = Color.white;

    static readonly Vector2Int[] Dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

    SpriteRenderer sr;
    float frightenedTimer;

    protected override void Awake()
    {
        base.Awake();
        sr = GetComponent<SpriteRenderer>();
    }

    void LateUpdate()
    {
        if (CurrentState != State.Frightened) return;
        frightenedTimer -= Time.deltaTime;
        if (frightenedTimer <= 0f) ResetState();
    }

    public void SetFrightened(float duration)
    {
        if (CurrentState == State.Eaten) return;
        CurrentState = State.Frightened;
        frightenedTimer = duration;
        speed = GameManager.GhostFrightenedSpeed;
        sr.color = Color.blue;
    }

    public void SetEaten()
    {
        CurrentState = State.Eaten;
        speed = GameManager.GhostEatenSpeed;
        sr.color = baseColor;
    }

    public void ResetState()
    {
        CurrentState = State.Chase;
        speed = GameManager.GhostChaseSpeed;
        sr.color = baseColor;
    }

    protected override Vector2Int GetDesiredDirection()
    {
        if (CurrentState == State.Eaten)
        {
            if (Cell == homeCell)
                ResetState();
            return GreedyDirection(homeCell);
        }

        if (CurrentState == State.Frightened)
            return RandomOpenDirection();

        return GreedyDirection(GameManager.Instance.PacmanCell);
    }

    // ponytail: one shared greedy-distance heuristic for all ghosts, not per-ghost personalities
    Vector2Int GreedyDirection(Vector2Int target)
    {
        Vector2Int best = -Direction;
        float bestDist = float.MaxValue;
        bool found = false;

        foreach (var d in Dirs)
        {
            if (d == -Direction) continue;
            if (GameManager.Instance.IsWall(Cell + d)) continue;

            float dist = (target - (Cell + d)).sqrMagnitude;
            if (!found || dist < bestDist)
            {
                bestDist = dist;
                best = d;
                found = true;
            }
        }

        return best;
    }

    Vector2Int RandomOpenDirection()
    {
        var options = new List<Vector2Int>();
        foreach (var d in Dirs)
        {
            if (d == -Direction) continue;
            if (!GameManager.Instance.IsWall(Cell + d)) options.Add(d);
        }

        if (options.Count == 0) return -Direction;
        return options[Random.Range(0, options.Count)];
    }
}
