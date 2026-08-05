using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public GameObject pacmanPrefab;
    public GameObject wallPrefab;
    public GameObject pelletPrefab;
    public GameObject[] ghostPrefabs;

    public const int Width = 19;
    public const int Height = 21;
    public const int TunnelRow = 10;

    const int PelletScore = 10;
    const int PowerPelletScore = 50;
    const int GhostEatScore = 200;
    const int StartLives = 3;
    const float FrightenedDuration = 7f;
    const float DeathFreeze = 1f;

    public const float PacmanSpeed = 8f;
    public const float GhostChaseSpeed = 7f;
    public const float GhostFrightenedSpeed = 5f;
    public const float GhostEatenSpeed = 10f;

    const float PelletUnitSize = 0.25f;
    const float PowerPelletUnitSize = 0.6f;

    static readonly Vector2Int PacmanSpawnCell = new Vector2Int(9, 1);
    static readonly Vector2Int GhostHouseCenter = new Vector2Int(9, 10);
    static readonly Vector2Int[] GhostSpawnCells =
    {
        new Vector2Int(9, 9), new Vector2Int(8, 10), new Vector2Int(10, 10), new Vector2Int(9, 11)
    };

    // ponytail: open arena w/ symmetric pillar obstacles, not a hand-typed dense classic maze
    static readonly Vector2Int[] PillarOrigins =
    {
        new Vector2Int(2, 2), new Vector2Int(14, 2),
        new Vector2Int(2, 7), new Vector2Int(14, 7),
        new Vector2Int(2, 12), new Vector2Int(14, 12),
        new Vector2Int(2, 16), new Vector2Int(14, 16),
        new Vector2Int(8, 2), new Vector2Int(8, 16),
    };

    static readonly Vector2Int[] PowerPelletCells =
    {
        new Vector2Int(1, 1), new Vector2Int(Width - 2, 1),
        new Vector2Int(1, Height - 2), new Vector2Int(Width - 2, Height - 2),
    };

    HashSet<Vector2Int> walls;
    HashSet<Vector2Int> ghostHouse;
    HashSet<Vector2Int> powerCells;
    Dictionary<Vector2Int, GameObject> pellets;
    int remainingPellets;

    PacmanController pacman;
    GhostController[] ghosts;

    int score;
    int lives = StartLives;
    float freezeTimer;
    bool gameOver;

    Text scoreText;
    Text livesText;
    Text messageText;

    public Vector2Int PacmanCell => pacman.Cell;

    void Awake()
    {
        Instance = this;
        BuildMaze();
        SpawnActors();
        FitCamera();
        BuildUI();
    }

    void Update()
    {
        if (gameOver)
        {
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            return;
        }

        if (freezeTimer > 0f)
        {
            freezeTimer -= Time.deltaTime;
            if (freezeTimer <= 0f) SetPaused(false);
            return;
        }

        var cell = pacman.Cell;

        if (pellets.TryGetValue(cell, out var pelletGO))
        {
            bool power = powerCells.Contains(cell);
            Destroy(pelletGO);
            pellets.Remove(cell);
            powerCells.Remove(cell);
            remainingPellets--;
            score += power ? PowerPelletScore : PelletScore;
            UpdateScoreText();

            if (power)
                foreach (var g in ghosts) g.SetFrightened(FrightenedDuration);

            if (remainingPellets <= 0)
            {
                EndGame("You win! Press R to restart");
                return;
            }
        }

        foreach (var g in ghosts)
        {
            if (g.Cell != cell) continue;

            if (g.CurrentState == GhostController.State.Frightened)
            {
                g.SetEaten();
                score += GhostEatScore;
                UpdateScoreText();
            }
            else if (g.CurrentState == GhostController.State.Chase)
            {
                LoseLife();
                break;
            }
        }
    }

    void BuildMaze()
    {
        walls = new HashSet<Vector2Int>();
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                bool border = x == 0 || x == Width - 1 || y == 0 || y == Height - 1;
                bool tunnelGap = y == TunnelRow && (x == 0 || x == Width - 1);
                if (border && !tunnelGap)
                    walls.Add(new Vector2Int(x, y));
            }
        }

        foreach (var origin in PillarOrigins)
            for (int dx = 0; dx < 3; dx++)
                for (int dy = 0; dy < 3; dy++)
                    walls.Add(new Vector2Int(origin.x + dx, origin.y + dy));

        ghostHouse = new HashSet<Vector2Int>();
        for (int x = 8; x <= 10; x++)
            for (int y = 9; y <= 11; y++)
                ghostHouse.Add(new Vector2Int(x, y));

        powerCells = new HashSet<Vector2Int>(PowerPelletCells);
        pellets = new Dictionary<Vector2Int, GameObject>();

        for (int x = 1; x < Width - 1; x++)
        {
            for (int y = 1; y < Height - 1; y++)
            {
                var cell = new Vector2Int(x, y);
                if (walls.Contains(cell) || ghostHouse.Contains(cell)) continue;

                var go = Instantiate(pelletPrefab, GridToWorld(cell), Quaternion.identity, transform);
                SetWorldSize(go.GetComponent<SpriteRenderer>(), powerCells.Contains(cell) ? PowerPelletUnitSize : PelletUnitSize);
                pellets[cell] = go;
            }
        }

        remainingPellets = pellets.Count;

        foreach (var cell in walls)
        {
            var go = Instantiate(wallPrefab, GridToWorld(cell), Quaternion.identity, transform);
            SetWorldSize(go.GetComponent<SpriteRenderer>(), 1f);
        }
    }

    void SpawnActors()
    {
        var pacmanGO = Instantiate(pacmanPrefab, GridToWorld(PacmanSpawnCell), Quaternion.identity);
        SetWorldSize(pacmanGO.GetComponent<SpriteRenderer>(), 1f);
        pacman = pacmanGO.AddComponent<PacmanController>();
        pacman.speed = PacmanSpeed;
        pacman.WarpTo(PacmanSpawnCell);

        ghosts = new GhostController[ghostPrefabs.Length];
        for (int i = 0; i < ghostPrefabs.Length; i++)
        {
            var cell = GhostSpawnCells[i % GhostSpawnCells.Length];
            var go = Instantiate(ghostPrefabs[i], GridToWorld(cell), Quaternion.identity);
            SetWorldSize(go.GetComponent<SpriteRenderer>(), 1f);
            var ghost = go.AddComponent<GhostController>();
            ghost.baseColor = go.GetComponent<SpriteRenderer>().color;
            ghost.speed = GhostChaseSpeed;
            ghost.homeCell = GhostHouseCenter;
            ghost.WarpTo(cell);
            ghosts[i] = ghost;
        }
    }

    void FitCamera()
    {
        var cam = Camera.main;
        if (cam == null) return;

        cam.orthographic = true;
        float halfHeight = Height / 2f;
        float halfWidth = Width / 2f / cam.aspect;
        cam.orthographicSize = Mathf.Max(halfHeight, halfWidth) + 1f;

        var t = cam.transform;
        t.position = new Vector3(0f, 0f, t.position.z);
    }

    void BuildUI()
    {
        var canvasGO = new GameObject("HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGO.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

        scoreText = CreateText(canvasGO.transform, "ScoreText", new Vector2(0, 1), new Vector2(20, -20), TextAnchor.UpperLeft);
        livesText = CreateText(canvasGO.transform, "LivesText", new Vector2(1, 1), new Vector2(-20, -20), TextAnchor.UpperRight);
        messageText = CreateText(canvasGO.transform, "MessageText", new Vector2(0.5f, 0.5f), Vector2.zero, TextAnchor.MiddleCenter);
        messageText.fontSize = 48;
        messageText.gameObject.SetActive(false);

        UpdateScoreText();
        UpdateLivesText();
    }

    static Text CreateText(Transform parent, string name, Vector2 anchor, Vector2 pos, TextAnchor align)
    {
        var go = new GameObject(name, typeof(Text));
        go.transform.SetParent(parent, false);

        var text = go.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 28;
        text.alignment = align;
        text.color = Color.white;

        var rt = text.rectTransform;
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(320, 60);

        return text;
    }

    void LoseLife()
    {
        lives--;
        UpdateLivesText();

        if (lives <= 0)
        {
            EndGame("Game over! Press R to restart");
            return;
        }

        freezeTimer = DeathFreeze;
        SetPaused(true);
        pacman.WarpTo(PacmanSpawnCell);
        for (int i = 0; i < ghosts.Length; i++)
        {
            ghosts[i].ResetState();
            ghosts[i].WarpTo(GhostSpawnCells[i % GhostSpawnCells.Length]);
        }
    }

    void EndGame(string message)
    {
        gameOver = true;
        SetPaused(true);
        messageText.text = message;
        messageText.gameObject.SetActive(true);
    }

    void SetPaused(bool value)
    {
        pacman.paused = value;
        foreach (var g in ghosts) g.paused = value;
    }

    void UpdateScoreText() => scoreText.text = $"Score: {score}";
    void UpdateLivesText() => livesText.text = $"Lives: {lives}";

    static void SetWorldSize(SpriteRenderer sr, float unitSize)
    {
        Vector2 size = sr.sprite.bounds.size;
        sr.transform.localScale = new Vector3(unitSize / size.x, unitSize / size.y, 1f);
    }

    public bool IsWall(Vector2Int cell)
    {
        if (cell.y == TunnelRow && (cell.x == -1 || cell.x == Width))
            return false;
        if (cell.x < 0 || cell.x >= Width || cell.y < 0 || cell.y >= Height)
            return true;
        return walls.Contains(cell);
    }

    public Vector2Int WrapCell(Vector2Int cell)
    {
        if (cell.x < 0) cell.x = Width - 1;
        else if (cell.x >= Width) cell.x = 0;
        return cell;
    }

    public Vector2 GridToWorld(Vector2Int cell)
    {
        return new Vector2(cell.x - (Width - 1) / 2f, cell.y - (Height - 1) / 2f);
    }
}
