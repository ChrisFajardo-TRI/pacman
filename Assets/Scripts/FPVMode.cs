using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// First-person view: perspective camera rides Pac-Man, walls rendered as cubes,
// sprites billboarded. Toggle with V key or two-finger tap. Pure presentation —
// game logic, movement, and collisions are untouched.
public class FPVMode : MonoBehaviour
{
    public static FPVMode Instance { get; private set; }

    public bool Active { get; private set; }
    public Vector2Int Facing => lastDir;

    GameObject worldRoot;               // cubes + floor, only active in FPV
    readonly List<Renderer> cubeRenderers = new List<Renderer>();
    Material wallMat;

    PacmanController pacman;
    SpriteRenderer pacmanRenderer;
    GhostController[] ghosts;
    Transform spriteParent;             // GameManager transform (pellets, fruit, walls)

    Camera cam;
    float cam2DZ;
    Vector2Int lastDir = Vector2Int.right;

    const float Fov = 75f;
    const float TurnSpeed = 10f;

    public void Init(IEnumerable<Vector2Int> walls, Color wallColor, PacmanController pac, Transform sprites)
    {
        Instance = this;
        RenderSettings.fog = false; // scene bakes it on for variant inclusion; off in 2D
        pacman = pac;
        pacmanRenderer = pac.GetComponent<SpriteRenderer>();
        spriteParent = sprites;
        cam = Camera.main;
        cam2DZ = cam.transform.position.z;

        // Prefabs from Resources/ so shader + meshes survive build stripping.
        var wallPrefab = Resources.Load<GameObject>("FPVWall");
        var floorPrefab = Resources.Load<GameObject>("FPVFloor");
        wallMat = new Material(wallPrefab.GetComponent<MeshRenderer>().sharedMaterial);
        SetWallColor(wallColor);

        worldRoot = new GameObject("FPVWorld");
        worldRoot.transform.SetParent(transform, false);

        foreach (var cell in walls)
        {
            var cube = Instantiate(wallPrefab, worldRoot.transform);
            cube.transform.position = GameManager.Instance.GridToWorld(cell);
            var r = cube.GetComponent<Renderer>();
            r.sharedMaterial = wallMat;
            cubeRenderers.Add(r);
        }

        var floor = Instantiate(floorPrefab, worldRoot.transform);
        floor.transform.position = new Vector3(0f, 0f, 0.5f);
        floor.transform.localScale = new Vector3(GameManager.Width + 4, GameManager.Height + 4, 1f);

        worldRoot.SetActive(false);
    }

    public void SetWallColor(Color c) => wallMat.SetColor("_BaseColor", c);

    void Update()
    {
        var kb = Keyboard.current;
        bool toggleKey = kb != null && kb.vKey.wasPressedThisFrame;
        var ts = Touchscreen.current;
        bool toggleTap = ts != null && ts.touches.Count > 1 &&
                         ts.touches[1].press.wasPressedThisFrame && ts.touches[0].press.isPressed;
        if (toggleKey || toggleTap) Toggle();

        if (!Active) return;

        if (pacman.Direction != Vector2Int.zero) lastDir = pacman.Direction;

        Vector3 fwd = new Vector3(lastDir.x, lastDir.y, 0f);
        Quaternion target = Quaternion.LookRotation(fwd, Vector3.back);
        cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, target, TurnSpeed * Time.deltaTime);
        cam.transform.position = pacman.transform.position;

        Billboard(cam.transform.rotation);
    }

    void Toggle()
    {
        Active = !Active;
        worldRoot.SetActive(Active);
        pacmanRenderer.enabled = !Active;

        // fog = only depth cue with unlit walls; scene has fog baked ON so the
        // shader variants survive build stripping — we just flip it per mode
        RenderSettings.fog = Active;
        if (Active)
        {
            if (ghosts == null) ghosts = FindObjectsByType<GhostController>();
            cam.orthographic = false;
            cam.fieldOfView = Fov;
            cam.nearClipPlane = 0.05f;
            cam.transform.position = pacman.transform.position;
            Vector2Int d = pacman.Direction != Vector2Int.zero ? pacman.Direction : lastDir;
            lastDir = d;
            cam.transform.rotation = Quaternion.LookRotation(new Vector3(d.x, d.y, 0f), Vector3.back);
        }
        else
        {
            Billboard(Quaternion.identity);
            cam.transform.rotation = Quaternion.identity;
            cam.transform.position = new Vector3(0f, 0f, cam2DZ); // FPV moved us to z=0; FitCamera keeps z
            GameManager.Instance.FitCamera();
        }
    }

    void Billboard(Quaternion rot)
    {
        foreach (Transform child in spriteParent)
            if (child.TryGetComponent<SpriteRenderer>(out _))
                child.rotation = rot;
        if (ghosts != null)
            foreach (var g in ghosts)
                if (g != null) g.transform.rotation = rot;
    }
}
