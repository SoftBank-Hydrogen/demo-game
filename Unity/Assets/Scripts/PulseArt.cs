using UnityEngine;
using UnityEngine.UI;

// Small static background mesh. Rebuilt on layout changes, never explicitly each frame.
public class PulseArt : MaskableGraphic
{
    static Color Hex(string value) { ColorUtility.TryParseHtmlString(value, out var c); return c; }
    static Color Alpha(Color c, float a) { c.a = a; return c; }
    readonly Color mint = Hex("#5CF2C4");
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        var r = rectTransform.rect;
        {
            Quad(vh, new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMax), Hex("#04070B"), Hex("#0A171D"));
            // Quiet, deterministic star field. Never uses Unity's shared random state.
            var rng = new System.Random(27);
            for (int i = 0; i < 95; i++)
            {
                float x = Mathf.Lerp(r.xMin, r.xMax, (float)rng.NextDouble());
                float y = Mathf.Lerp(r.yMin, r.yMax, (float)rng.NextDouble());
                float size = i % 7 == 0 ? 1.5f : .7f;
                Quad(vh, new Vector2(x, y), new Vector2(x + size, y + size), Alpha(mint, i % 7 == 0 ? .35f : .10f));
            }
            return;
        }
    }
    static void Quad(VertexHelper vh, Vector2 min, Vector2 max, Color c) => Quad(vh, min, max, c, c);
    static void Quad(VertexHelper vh, Vector2 min, Vector2 max, Color bottom, Color top)
    {
        int n = vh.currentVertCount;
        vh.AddVert(new Vector3(min.x, min.y), bottom, Vector2.zero);
        vh.AddVert(new Vector3(min.x, max.y), top, Vector2.zero);
        vh.AddVert(new Vector3(max.x, max.y), top, Vector2.zero);
        vh.AddVert(new Vector3(max.x, min.y), bottom, Vector2.zero);
        vh.AddTriangle(n, n + 1, n + 2); vh.AddTriangle(n, n + 2, n + 3);
    }
}
