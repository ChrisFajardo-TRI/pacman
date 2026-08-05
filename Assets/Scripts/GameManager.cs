using System.Collections;
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

    public enum Mode { Scatter, Chase }

    // Classic-style maze. '#' wall, '.' pellet, 'o' power pellet, '-' ghost door,
    // ' ' walkable but no pellet. Row 0 = top of screen.
    static readonly string[] Map =
    {
        "############################",
        "#............##............#",
        "#.####.#####.##.#####.####.#",
        "#o####.#####.##.#####.####o#",
        "#.####.#####.##.#####.####.#",
        "#..........................#",
        "#.####.##.########.##.####.#",
        "#.####.##.########.##.####.#",
        "#......##....##....##......#",
        "######.##### ## #####.######",
        "     #.##### ## #####.#     ",
        "     #.##          ##.#     ",
        "     #.## ###--### ##.#     ",
        "######.## #      # ##.######",
        "      .   #      #   .      ",
        "######.## #      # ##.######",
        "     #.## ######## ##.#     ",
        "     #.##          ##.#     ",
        "     #.## ######## ##.#     ",
        "######.## ######## ##.######",
        "#............##............#",
        "#.####.#####.##.#####.####.#",
        "#.####.#####.##.#####.####.#",
        "#o..##.......  .......##..o#",
        "###.##.##.########.##.##.###",
        "###.##.##.########.##.##.###",
        "#......##....##....##......#",
        "#.##########.##.##########.#",
        "#.##########.##.##########.#",
        "#..........................#",
        "############################",
    };

    public static int Width => Map[0].Length;   // 28
    public static int Height => Map.Length;     // 31
    static int TunnelRowY => Height - 1 - 14;

    const int PelletScore = 10;
    const int PowerPelletScore = 50;
    const int StartLives = 3;
    const int ExtraLifeScore = 10000;

    static Vector2Int FromMap(int col, int row) => new Vector2Int(col, Height - 1 - row);

    static readonly Vector2Int PacmanSpawnCell = new Vector2Int(13, Height - 1 - 23);
    static readonly Vector2Int FruitCell = new Vector2Int(13, Height - 1 - 17);
    public Vector2Int GhostExitCell => new Vector2Int(13, Height - 1 - 11);
    public Vector2Int GhostHomeCell => new Vector2Int(13, Height - 1 - 14);

    static readonly Color WallBlue = new Color(0.13f, 0.13f, 0.87f);
    static readonly Color[] LevelWallColors =
    {
        WallBlue,
        new Color(0.1f, 0.6f, 0.6f),
        new Color(0.55f, 0.2f, 0.75f),
        new Color(0.8f, 0.45f, 0.1f),
        new Color(0.7f, 0.15f, 0.3f),
    };
    static readonly Color PelletColor = new Color(1f, 0.72f, 0.68f);

    // (mode, duration) waves; last chase runs forever
    static readonly (Mode mode, float dur)[] ModeSchedule =
    {
        (Mode.Scatter, 7f), (Mode.Chase, 20f),
        (Mode.Scatter, 7f), (Mode.Chase, 20f),
        (Mode.Scatter, 5f), (Mode.Chase, float.PositiveInfinity),
    };

    HashSet<Vector2Int> walls;
    HashSet<Vector2Int> doors;
    HashSet<Vector2Int> powerCells;
    Dictionary<Vector2Int, GameObject> pellets;
    readonly List<SpriteRenderer> wallRenderers = new List<SpriteRenderer>();
    readonly List<SpriteRenderer> powerPelletRenderers = new List<SpriteRenderer>();
    int remainingPellets;
    int totalPellets;
    int pelletsEaten;

    PacmanController pacman;
    GhostController[] ghosts;
    GhostController blinky;

    int score;
    int highScore;
    int lives = StartLives;
    int level = 1;
    int ghostCombo;
    bool extraLifeAwarded;
    bool gameOver;
    bool roundActive;
    int wakaToggle;

    public Mode GlobalMode { get; private set; } = Mode.Scatter;
    int modePhase;
    float modeTimer;

    GameObject fruit;
    float fruitTimer;
    int fruitSpawnsThisLevel;

    Text scoreText;
    Text highScoreText;
    Text livesText;
    Text levelText;
    Text messageText;

    AudioSource sfx;
    AudioSource siren;
    AudioClip waka1, waka2, powerClip, ghostEatenClip, deathClip, fruitClip, extraLifeClip;

    public Vector2Int PacmanCell => pacman.Cell;
    public Vector2Int PacmanDirection => pacman.Direction;
    public Vector2Int BlinkyCell => blinky.Cell;

    const float PelletUnitSize = 0.25f;
    const float PowerPelletUnitSize = 0.6f;

    void Awake()
    {
        Instance = this;
        highScore = PlayerPrefs.GetInt("HighScore", 0);
        BuildStatic();
        BuildPellets();
        SpawnActors();
        FitCamera();
        BuildUI();
        BuildAudio();
    }

    void Start()
    {
        StartCoroutine(StartRound());
    }

    void Update()
    {
        if (gameOver)
        {
            bool restartKey = Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame;
            bool restartTap = Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame;
            if (restartKey || restartTap)
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            return;
        }

        if (!roundActive) return;

        ModeTick();
        FruitTick();
        PowerPelletBlink();
        siren.pitch = AnyFrightened() ? 1.5f : 1f + 0.05f * (level - 1);

        EatPellets();
        if (!roundActive) return; // level cleared inside EatPellets

        CheckGhostCollisions();
        CheckFruitPickup();
    }

    // ---------- gameplay ----------

    void EatPellets()
    {
        var cell = pacman.Cell;
        if (!pellets.TryGetValue(cell, out var pelletGO)) return;

        bool power = powerCells.Contains(cell);
        powerPelletRenderers.Remove(pelletGO.GetComponent<SpriteRenderer>());
        Destroy(pelletGO);
        pellets.Remove(cell);
        powerCells.Remove(cell);
        remainingPellets--;
        pelletsEaten++;
        AddScore(power ? PowerPelletScore : PelletScore);

        wakaToggle ^= 1;
        sfx.PlayOneShot(wakaToggle == 0 ? waka1 : waka2);

        if (power)
        {
            ghostCombo = 0;
            foreach (var g in ghosts) g.SetFrightened(FrightenedDuration());
            sfx.PlayOneShot(powerClip);
        }

        // fruit at 1/3 and 2/3 of pellets eaten
        if (fruit == null && fruitSpawnsThisLevel < 2 &&
            pelletsEaten >= totalPellets * (fruitSpawnsThisLevel + 1) / 3)
            SpawnFruit();

        if (remainingPellets <= 0)
        {
            roundActive = false;
            StartCoroutine(LevelClear());
        }
    }

    void CheckGhostCollisions()
    {
        Vector2 pacPos = pacman.transform.position;
        foreach (var g in ghosts)
        {
            if (Vector2.Distance(pacPos, g.transform.position) > 0.7f) continue;

            if (g.CurrentState == GhostController.State.Frightened)
            {
                int points = 200 << ghostCombo;
                ghostCombo = Mathf.Min(ghostCombo + 1, 3);
                g.SetEaten();
                AddScore(points);
                ScorePopup.Spawn(g.transform.position, points.ToString(), Color.cyan);
                sfx.PlayOneShot(ghostEatenClip);
                StartCoroutine(MicroFreeze());
            }
            else if (g.CurrentState == GhostController.State.Active || g.CurrentState == GhostController.State.Leaving)
            {
                roundActive = false;
                StartCoroutine(Death());
                return;
            }
        }
    }

    void SpawnFruit()
    {
        fruitSpawnsThisLevel++;
        fruit = Instantiate(pelletPrefab, GridToWorld(FruitCell), Quaternion.identity, transform);
        var sr = fruit.GetComponent<SpriteRenderer>();
        sr.color = new Color(0.95f, 0.15f, 0.15f);
        SetWorldSize(sr, 0.8f);
        fruitTimer = 10f;
    }

    void FruitTick()
    {
        if (fruit == null) return;
        fruitTimer -= Time.deltaTime;
        if (fruitTimer <= 0f) { Destroy(fruit); fruit = null; }
    }

    void CheckFruitPickup()
    {
        if (fruit == null) return;
        if (Vector2.Distance(pacman.transform.position, fruit.transform.position) > 0.7f) return;

        int points = Mathf.Min(100 * level, 5000);
        AddScore(points);
        ScorePopup.Spawn(fruit.transform.position, points.ToString(), new Color(1f, 0.5f, 0.5f));
        sfx.PlayOneShot(fruitClip);
        Destroy(fruit);
        fruit = null;
    }

    void ModeTick()
    {
        modeTimer += Time.deltaTime;
        if (modeTimer < ModeSchedule[modePhase].dur) return;

        modeTimer = 0f;
        modePhase = Mathf.Min(modePhase + 1, ModeSchedule.Length - 1);
        GlobalMode = ModeSchedule[modePhase].mode;
        foreach (var g in ghosts) g.NotifyModeChanged();
    }

    void AddScore(int amount)
    {
        score += amount;
        if (!extraLifeAwarded && score >= ExtraLifeScore)
        {
            extraLifeAwarded = true;
            lives++;
            UpdateLivesText();
            ScorePopup.Spawn(pacman.transform.position, "1UP!", Color.yellow);
            sfx.PlayOneShot(extraLifeClip);
        }
        if (score > highScore)
        {
            highScore = score;
            UpdateHighScoreText();
        }
        UpdateScoreText();
    }

    bool AnyFrightened()
    {
        foreach (var g in ghosts)
            if (g.CurrentState == GhostController.State.Frightened) return true;
        return false;
    }

    // ---------- level / round flow ----------

    float FrightenedDuration() => Mathf.Max(7f - (level - 1), 2f);
    public float PacmanSpeed() => Mathf.Min(8f * (1f + 0.03f * (level - 1)), 10.5f);

    public float GhostSpeed(GhostController.State state)
    {
        switch (state)
        {
            case GhostController.State.Frightened: return 5f;
            case GhostController.State.Eaten: return 14f;
            case GhostController.State.Leaving: return 4f;
            default: return Mathf.Min(7f * (1f + 0.05f * (level - 1)), 9.8f);
        }
    }

    IEnumerator StartRound()
    {
        SetPaused(true);
        siren.Stop();
        ShowMessage("READY!", Color.yellow);
        yield return new WaitForSeconds(2f);
        HideMessage();
        modePhase = 0;
        modeTimer = 0f;
        GlobalMode = ModeSchedule[0].mode;
        SetPaused(false);
        roundActive = true;
        siren.Play();
    }

    IEnumerator Death()
    {
        SetPaused(true);
        siren.Stop();
        sfx.PlayOneShot(deathClip);

        // spin & shrink
        var t = pacman.transform;
        Vector3 startScale = t.localScale;
        float dur = 1.3f;
        for (float e = 0f; e < dur; e += Time.deltaTime)
        {
            float p = e / dur;
            t.rotation = Quaternion.Euler(0, 0, p * 720f);
            t.localScale = startScale * (1f - p);
            yield return null;
        }
        t.localScale = Vector3.zero;

        lives--;
        UpdateLivesText();

        if (lives <= 0)
        {
            gameOver = true;
            PlayerPrefs.SetInt("HighScore", highScore);
            PlayerPrefs.Save();
            ShowMessage("GAME OVER\nPress R or tap to restart", Color.red);
            yield break;
        }

        yield return new WaitForSeconds(0.5f);
        ResetActors();
        yield return StartRound();
    }

    IEnumerator LevelClear()
    {
        SetPaused(true);
        siren.Stop();
        if (fruit != null) { Destroy(fruit); fruit = null; }

        Color levelColor = LevelWallColors[(level - 1) % LevelWallColors.Length];
        for (int i = 0; i < 6; i++)
        {
            foreach (var sr in wallRenderers)
                sr.color = i % 2 == 0 ? Color.white : levelColor;
            yield return new WaitForSeconds(0.25f);
        }

        level++;
        UpdateLevelText();
        Color newColor = LevelWallColors[(level - 1) % LevelWallColors.Length];
        foreach (var sr in wallRenderers) sr.color = newColor;

        BuildPellets();
        ResetActors();
        yield return StartRound();
    }

    IEnumerator MicroFreeze()
    {
        SetPaused(true);
        yield return new WaitForSeconds(0.25f);
        if (!gameOver && roundActive) SetPaused(false);
    }

    void ResetActors()
    {
        pacman.WarpTo(PacmanSpawnCell);
        pacman.ResetVisual();
        pacman.speed = PacmanSpeed();
        foreach (var g in ghosts) g.ResetRound();
    }

    // ---------- construction ----------

    void BuildStatic()
    {
        walls = new HashSet<Vector2Int>();
        doors = new HashSet<Vector2Int>();

        for (int row = 0; row < Height; row++)
        {
            string line = Map[row];
            for (int col = 0; col < Width; col++)
            {
                char c = col < line.Length ? line[col] : ' ';
                var cell = FromMap(col, row);
                if (c == '#') walls.Add(cell);
                else if (c == '-') doors.Add(cell);
            }
        }

        Color wallColor = LevelWallColors[0];
        foreach (var cell in walls)
        {
            var go = Instantiate(wallPrefab, GridToWorld(cell), Quaternion.identity, transform);
            var sr = go.GetComponent<SpriteRenderer>();
            SetWorldSize(sr, 1f);
            sr.color = wallColor;
            wallRenderers.Add(sr);
        }

        foreach (var cell in doors)
        {
            var go = Instantiate(wallPrefab, GridToWorld(cell), Quaternion.identity, transform);
            var sr = go.GetComponent<SpriteRenderer>();
            sr.color = new Color(1f, 0.7f, 0.85f);
            SetWorldSize(sr, 1f);
            go.transform.localScale = new Vector3(go.transform.localScale.x, go.transform.localScale.y * 0.25f, 1f);
        }
    }

    void BuildPellets()
    {
        pellets = new Dictionary<Vector2Int, GameObject>();
        powerCells = new HashSet<Vector2Int>();
        powerPelletRenderers.Clear();
        pelletsEaten = 0;
        fruitSpawnsThisLevel = 0;

        for (int row = 0; row < Height; row++)
        {
            string line = Map[row];
            for (int col = 0; col < Width; col++)
            {
                char c = col < line.Length ? line[col] : ' ';
                if (c != '.' && c != 'o') continue;

                var cell = FromMap(col, row);
                var go = Instantiate(pelletPrefab, GridToWorld(cell), Quaternion.identity, transform);
                var sr = go.GetComponent<SpriteRenderer>();
                sr.color = PelletColor;
                bool power = c == 'o';
                SetWorldSize(sr, power ? PowerPelletUnitSize : PelletUnitSize);
                if (power)
                {
                    powerCells.Add(cell);
                    powerPelletRenderers.Add(sr);
                }
                pellets[cell] = go;
            }
        }

        remainingPellets = pellets.Count;
        totalPellets = pellets.Count;
    }

    void PowerPelletBlink()
    {
        bool on = Mathf.FloorToInt(Time.time * 4f) % 2 == 0;
        foreach (var sr in powerPelletRenderers)
            if (sr != null) sr.enabled = on;
    }

    void SpawnActors()
    {
        var pacmanGO = Instantiate(pacmanPrefab, GridToWorld(PacmanSpawnCell), Quaternion.identity);
        SetWorldSize(pacmanGO.GetComponent<SpriteRenderer>(), 1f);
        pacman = pacmanGO.AddComponent<PacmanController>();
        pacman.speed = PacmanSpeed();
        pacman.WarpTo(PacmanSpawnCell);

        var configs = new (GhostController.Personality p, Vector2Int spawn, Vector2Int corner, bool outside, float delay)[]
        {
            (GhostController.Personality.Blinky, GhostExitCell, new Vector2Int(Width - 2, Height - 2), true, 0f),
            (GhostController.Personality.Pinky, GhostHomeCell, new Vector2Int(1, Height - 2), false, 1.5f),
            (GhostController.Personality.Inky, GhostHomeCell + Vector2Int.left * 2, new Vector2Int(Width - 2, 1), false, 5f),
            (GhostController.Personality.Clyde, GhostHomeCell + Vector2Int.right * 2, new Vector2Int(1, 1), false, 9f),
        };

        int count = Mathf.Min(ghostPrefabs.Length, configs.Length);
        ghosts = new GhostController[count];
        for (int i = 0; i < count; i++)
        {
            var cfg = configs[i];
            var go = Instantiate(ghostPrefabs[i], GridToWorld(cfg.spawn), Quaternion.identity);
            SetWorldSize(go.GetComponent<SpriteRenderer>(), 1f);
            var ghost = go.AddComponent<GhostController>();
            ghost.baseColor = go.GetComponent<SpriteRenderer>().color;
            ghost.personality = cfg.p;
            ghost.spawnCell = cfg.spawn;
            ghost.scatterCorner = cfg.corner;
            ghost.startsOutside = cfg.outside;
            ghost.releaseDelay = cfg.delay;
            ghost.ResetRound();
            ghosts[i] = ghost;
            if (cfg.p == GhostController.Personality.Blinky) blinky = ghost;
        }
        if (blinky == null) blinky = ghosts[0];
    }

    void FitCamera()
    {
        var cam = Camera.main;
        if (cam == null) return;

        cam.orthographic = true;
        cam.backgroundColor = Color.black;
        float halfHeight = Height / 2f;
        float halfWidth = Width / 2f / cam.aspect;
        cam.orthographicSize = Mathf.Max(halfHeight, halfWidth) + 1.5f;

        var t = cam.transform;
        t.position = new Vector3(0f, 0f, t.position.z);
    }

    // ---------- UI ----------

    void BuildUI()
    {
        var canvasGO = new GameObject("HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGO.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

        scoreText = CreateText(canvasGO.transform, "ScoreText", new Vector2(0, 1), new Vector2(20, -20), TextAnchor.UpperLeft);
        highScoreText = CreateText(canvasGO.transform, "HighScoreText", new Vector2(0.5f, 1), new Vector2(0, -20), TextAnchor.UpperCenter);
        livesText = CreateText(canvasGO.transform, "LivesText", new Vector2(1, 1), new Vector2(-20, -20), TextAnchor.UpperRight);
        levelText = CreateText(canvasGO.transform, "LevelText", new Vector2(1, 1), new Vector2(-20, -55), TextAnchor.UpperRight);
        messageText = CreateText(canvasGO.transform, "MessageText", new Vector2(0.5f, 0.5f), Vector2.zero, TextAnchor.MiddleCenter);
        messageText.fontSize = 48;
        messageText.rectTransform.sizeDelta = new Vector2(800, 200);
        messageText.gameObject.SetActive(false);

        UpdateScoreText();
        UpdateHighScoreText();
        UpdateLivesText();
        UpdateLevelText();
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

    void ShowMessage(string msg, Color color)
    {
        messageText.text = msg;
        messageText.color = color;
        messageText.gameObject.SetActive(true);
    }

    void HideMessage() => messageText.gameObject.SetActive(false);

    void UpdateScoreText() => scoreText.text = $"Score: {score}";
    void UpdateHighScoreText() => highScoreText.text = $"High: {highScore}";
    void UpdateLivesText() => livesText.text = $"Lives: {lives}";
    void UpdateLevelText() => levelText.text = $"Level {level}";

    // ---------- audio ----------

    void BuildAudio()
    {
        sfx = gameObject.AddComponent<AudioSource>();
        sfx.playOnAwake = false;

        siren = gameObject.AddComponent<AudioSource>();
        siren.playOnAwake = false;
        siren.loop = true;
        siren.clip = AudioSynth.SirenLoop("siren", 380f, 560f, 0.9f);

        waka1 = AudioSynth.Sweep("waka1", 520f, 320f, 0.07f, 0.25f);
        waka2 = AudioSynth.Sweep("waka2", 320f, 520f, 0.07f, 0.25f);
        powerClip = AudioSynth.Sweep("power", 200f, 900f, 0.35f, 0.3f);
        ghostEatenClip = AudioSynth.Arpeggio("ghostEaten", new[] { 400f, 600f, 800f, 1200f }, 0.06f);
        deathClip = AudioSynth.Sweep("death", 620f, 80f, 1.2f, 0.35f);
        fruitClip = AudioSynth.Arpeggio("fruit", new[] { 800f, 1200f }, 0.09f);
        extraLifeClip = AudioSynth.Arpeggio("extraLife", new[] { 600f, 800f, 1000f, 1300f, 1600f }, 0.08f);
    }

    // ---------- grid helpers ----------

    void SetPaused(bool value)
    {
        pacman.paused = value;
        foreach (var g in ghosts) g.paused = value;
    }

    static void SetWorldSize(SpriteRenderer sr, float unitSize)
    {
        Vector2 size = sr.sprite.bounds.size;
        sr.transform.localScale = new Vector3(unitSize / size.x, unitSize / size.y, 1f);
    }

    public bool IsWall(Vector2Int cell)
    {
        if (cell.y == TunnelRowY && (cell.x == -1 || cell.x == Width))
            return false;
        if (cell.x < 0 || cell.x >= Width || cell.y < 0 || cell.y >= Height)
            return true;
        return walls.Contains(cell) || doors.Contains(cell);
    }

    public bool IsWallForGhost(Vector2Int cell, bool canUseDoor)
    {
        if (canUseDoor && doors.Contains(cell)) return false;
        return IsWall(cell);
    }

    public bool IsTunnel(Vector2Int cell)
    {
        return cell.y == TunnelRowY && (cell.x < 6 || cell.x > Width - 7);
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
