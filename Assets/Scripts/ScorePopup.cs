using UnityEngine;

public class ScorePopup : MonoBehaviour
{
    public static void Spawn(Vector2 pos, string text, Color color)
    {
        var go = new GameObject("ScorePopup");
        go.transform.position = new Vector3(pos.x, pos.y, -1f);
        var tm = go.AddComponent<TextMesh>();
        tm.text = text;
        tm.color = color;
        tm.fontSize = 48;
        tm.characterSize = 0.12f;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        go.GetComponent<MeshRenderer>().sortingOrder = 20;
        go.AddComponent<ScorePopup>();
    }

    float t;

    void Update()
    {
        t += Time.deltaTime;
        transform.position += Vector3.up * (1.4f * Time.deltaTime);
        var tm = GetComponent<TextMesh>();
        var c = tm.color;
        c.a = 1f - t;
        tm.color = c;
        if (t >= 1f) Destroy(gameObject);
    }
}
