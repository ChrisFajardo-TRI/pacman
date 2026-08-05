using System.Collections.Generic;
using UnityEngine;

public class GhostController : GridMover
{
    public enum Personality { Blinky, Pinky, Inky, Clyde }
    public enum State { InHouse, Leaving, Active, Frightened, Eaten }

    public Personality personality;
    public Color baseColor = Color.white;
    public Vector2Int scatterCorner;
    public Vector2Int spawnCell;
    public bool startsOutside;
    public float releaseDelay;

    public State CurrentState { get; private set; } = State.Active;

    static readonly Vector2Int[] Dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
    static readonly Color FrightenedBlue = new Color(0.15f, 0.15f, 0.95f);

    SpriteRenderer sr;
    float frightenedTimer;
    float releaseTimer;
    float bounceT;
    bool pendingReverse;

    protected override void Awake()
    {
        base.Awake();
        sr = GetComponent<SpriteRenderer>();
    }

    public void ResetRound()
    {
        WarpTo(spawnCell);
        frightenedTimer = 0f;
        pendingReverse = false;
        overrideMovement = false;
        CurrentState = startsOutside ? State.Active : State.InHouse;
        releaseTimer = releaseDelay;
        ApplyVisual();
    }

    protected override void FrameTick()
    {
        if (paused) return;

        if (CurrentState == State.InHouse)
        {
            overrideMovement = true;
            bounceT += Time.deltaTime;
            Vector2 basePos = GameManager.Instance.GridToWorld(Cell);
            transform.position = basePos + Vector2.up * (Mathf.Sin(bounceT * 5f) * 0.25f);
            releaseTimer -= Time.deltaTime;
            if (releaseTimer <= 0f)
            {
                CurrentState = State.Leaving;
                overrideMovement = false;
            }
            return;
        }

        if (CurrentState == State.Frightened)
        {
            frightenedTimer -= Time.deltaTime;
            if (frightenedTimer <= 0f)
            {
                CurrentState = State.Active;
                ApplyVisual();
            }
            else if (frightenedTimer < 2f)
            {
                sr.color = Mathf.FloorToInt(frightenedTimer * 6f) % 2 == 0 ? Color.white : FrightenedBlue;
            }
        }
    }

    public void SetFrightened(float duration)
    {
        if (CurrentState != State.Active && CurrentState != State.Frightened) return;
        CurrentState = State.Frightened;
        frightenedTimer = duration;
        pendingReverse = true;
        ApplyVisual();
    }

    public void SetEaten()
    {
        CurrentState = State.Eaten;
        frightenedTimer = 0f;
        ApplyVisual();
    }

    public void NotifyModeChanged()
    {
        if (CurrentState == State.Active) pendingReverse = true;
    }

    void ApplyVisual()
    {
        switch (CurrentState)
        {
            case State.Frightened: sr.color = FrightenedBlue; break;
            case State.Eaten: sr.color = new Color(1f, 1f, 1f, 0.35f); break;
            default:
                sr.color = baseColor;
                break;
        }
    }

    protected override bool Blocked(Vector2Int cell)
    {
        bool canUseDoor = CurrentState == State.Eaten || CurrentState == State.Leaving;
        return GameManager.Instance.IsWallForGhost(cell, canUseDoor);
    }

    protected override float CurrentSpeed()
    {
        var gm = GameManager.Instance;
        float s = gm.GhostSpeed(CurrentState);
        if (CurrentState != State.Eaten && gm.IsTunnel(Cell)) s *= 0.55f;
        return s;
    }

    protected override Vector2Int GetDesiredDirection()
    {
        var gm = GameManager.Instance;
        Vector2Int exit = gm.GhostExitCell;
        Vector2Int home = gm.GhostHomeCell;

        switch (CurrentState)
        {
            case State.Leaving:
                if (Cell == exit)
                {
                    CurrentState = State.Active;
                    ApplyVisual();
                    return GreedyDirection(TargetCell(), false);
                }
                if (Cell.x != exit.x) return Cell.x < exit.x ? Vector2Int.right : Vector2Int.left;
                return Vector2Int.up;

            case State.Eaten:
                if (Cell == home)
                {
                    CurrentState = State.Leaving;
                    ApplyVisual();
                    return Vector2Int.up;
                }
                if (Cell.x == home.x && Cell.y <= exit.y && Cell.y > home.y)
                    return Vector2Int.down;
                return GreedyDirection(exit, false);

            case State.Frightened:
                return RandomOpenDirection();

            default:
                if (pendingReverse)
                {
                    pendingReverse = false;
                    var rev = -Direction;
                    if (rev != Vector2Int.zero && !Blocked(Cell + rev)) return rev;
                }
                return GreedyDirection(TargetCell(), false);
        }
    }

    Vector2Int TargetCell()
    {
        var gm = GameManager.Instance;
        if (gm.GlobalMode == GameManager.Mode.Scatter) return scatterCorner;

        Vector2Int pac = gm.PacmanCell;
        Vector2Int pdir = gm.PacmanDirection;

        switch (personality)
        {
            case Personality.Pinky:
                return pac + pdir * 4;
            case Personality.Inky:
            {
                Vector2Int mid = pac + pdir * 2;
                return mid * 2 - gm.BlinkyCell;
            }
            case Personality.Clyde:
                return (pac - Cell).sqrMagnitude > 64 ? pac : scatterCorner;
            default:
                return pac;
        }
    }

    Vector2Int GreedyDirection(Vector2Int target, bool allowReverse)
    {
        Vector2Int best = -Direction;
        float bestDist = float.MaxValue;
        bool found = false;

        foreach (var d in Dirs)
        {
            if (!allowReverse && d == -Direction) continue;
            if (Blocked(Cell + d)) continue;

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
            if (!Blocked(Cell + d)) options.Add(d);
        }

        if (options.Count == 0) return -Direction;
        return options[Random.Range(0, options.Count)];
    }
}
