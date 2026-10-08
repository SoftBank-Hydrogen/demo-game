using System;
using System.IO;
using UnityEditor;
using UnityEngine;

// Creates the Tug character images ONCE and saves them as normal PNG files in Assets/Art/Tug:
//   MochiA.png (team A, mint, faces right), MochiB.png (team B, amber, faces right; mirrored in game),
//   Marker.png (white "you" triangle),
//   Accessories/*.png (ribbon, sprout, party hat, glasses, flower; drawn over the body, same 256 canvas).
// The scene keeps serialized references to these files, so they can be edited by hand in any image
// editor afterwards. Existing files are never overwritten unless "Rebake ... (overwrite)" is chosen.
// Shapes are signed-distance functions sampled 4x per pixel; design units are 128 x 128.
public static class TugArtwork
{
    const string Folder = "Assets/Art/Tug";
    const int Size = 256;                        // pixels; drawing coordinates stay in 128 units
    const float Unit = Size / 128f;              // hands at y ≈ 58/128, feet at ≈ 8/128

    public static readonly string[] AccessoryNames = { "Ribbon", "Sprout", "PartyHat", "Glasses", "Flower" };

    public static Sprite Load(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(Folder + "/" + name + ".png");
    public static Sprite[] LoadAccessories() => Array.ConvertAll(AccessoryNames, n => Load("Accessories/" + n));

    [MenuItem("Tug/Create missing character images")]
    public static void Generate() => Generate(false);

    [MenuItem("Tug/Rebake character images (overwrite edits)")]
    static void Rebake()
    {
        if (Application.isBatchMode || EditorUtility.DisplayDialog("Rebake character images",
            "This replaces MochiA.png, MochiB.png, Marker.png and Accessories/*.png, including any manual edits.", "Overwrite", "Cancel"))
            Generate(true);
    }

    public static void Generate(bool overwrite)
    {
        Directory.CreateDirectory(Folder + "/Accessories");
        Bake("MochiA", overwrite, (x, y) => Mochi(x, y, Hex("#5CF2C4"), Hex("#2FB894")));
        Bake("MochiB", overwrite, (x, y) => Mochi(x, y, Hex("#FFB547"), Hex("#D9933A")));
        Bake("Marker", overwrite, (x, y) => Fill(Triangle(x, y, new Vector2(18, 108), new Vector2(110, 108), new Vector2(64, 36)) - 8f, Color.white));
        Bake("Accessories/Ribbon", overwrite, Ribbon);
        Bake("Accessories/Sprout", overwrite, Sprout);
        Bake("Accessories/PartyHat", overwrite, PartyHat);
        Bake("Accessories/Glasses", overwrite, Glasses);
        Bake("Accessories/Flower", overwrite, Flower);
        AssetDatabase.SaveAssets();
        Debug.Log("TUG_ART_READY");
    }

    // One complete character: shadow, outline, body with ears and arm, inner ears, face.
    static Color Mochi(float x, float y, Color main, Color dark)
    {
        var eye = Hex("#071214"); var blush = new Color(1f, .62f, .71f, .75f);
        float body = Union(Ellipse(x, y, 60, 46, 44, 36), Circle(x, y, 40, 86, 12), Circle(x, y, 74, 88, 12),
            Capsule(x, y, 92, 54, 116, 58, 6.5f));
        Color c = new Color(0, 0, 0, .35f * Mathf.Clamp01(-Ellipse(x, y, 60, 9, 40, 6) / 6f + .2f));   // ground shadow
        c = Over(c, Fill(body - 3f, new Color(.016f, .043f, .055f, .85f)));                            // outline
        c = Over(c, Fill(body, main));
        c = Over(c, Fill(Union(Circle(x, y, 40, 86, 6), Circle(x, y, 74, 88, 6)), dark));             // inner ears
        c = Over(c, Fill(Ellipse(x, y, 70, 37, 7, 4), blush));
        c = Over(c, Fill(Ellipse(x, y, 98, 38, 5.5f, 3.5f), blush));
        c = Over(c, Fill(RoundRect(x, y, 74, 50, 2.6f, 6, 2.6f), eye));
        c = Over(c, Fill(RoundRect(x, y, 88, 51, 2.6f, 6, 2.6f), eye));
        c = Over(c, Fill(Circle(x, y, 74.6f, 53.5f, 1.2f), Color.white));
        c = Over(c, Fill(Circle(x, y, 88.6f, 54.5f, 1.2f), Color.white));
        return c;
    }

    // ---------- accessories (Mochi coordinates: head top ≈ y 82, ears at x 40 and 74, eyes at x 74 and 88) ----------

    static Color Ribbon(float x, float y)
    {
        float wings = Union(Triangle(x, y, new Vector2(57, 96), new Vector2(46, 103), new Vector2(46, 89)),
            Triangle(x, y, new Vector2(57, 96), new Vector2(68, 103), new Vector2(68, 89))) - 1.5f;
        float knot = Circle(x, y, 57, 96, 3.6f);
        return Over(Outlined(Union(wings, knot), Hex("#FF9EC4")), Fill(knot, Hex("#E8739F")));
    }

    static Color Sprout(float x, float y)
    {
        float stem = Capsule(x, y, 57, 81, 57, 99, 1.6f);
        float leaves = Union(Ellipse(x, y, 50.5f, 102, 7, 3.4f), Ellipse(x, y, 63.5f, 104, 7, 3.4f));
        return Over(Outlined(Union(stem, leaves), Hex("#7BCB5A")), Fill(stem, Hex("#3F8F4C")));
    }

    static Color PartyHat(float x, float y)
    {
        float cone = Triangle(x, y, new Vector2(48, 82), new Vector2(68, 84), new Vector2(60, 112)) - 1f;
        float pom = Circle(x, y, 60, 113, 4.2f);
        float stripe = Mathf.Max(Capsule(x, y, 50, 92, 68, 93, 1.4f), cone);     // clipped to the cone
        var c = Over(Outlined(Union(cone, pom), Hex("#B49CFF")), Fill(stripe, Hex("#FFF6E0")));
        return Over(c, Fill(pom, Hex("#FFF6E0")));
    }

    static Color Glasses(float x, float y)
    {
        float lenses = Union(Circle(x, y, 74, 50.5f, 7), Circle(x, y, 88, 51, 7));
        float rims = Union(Mathf.Abs(Circle(x, y, 74, 50.5f, 7)) - 1.2f, Mathf.Abs(Circle(x, y, 88, 51, 7)) - 1.2f,
            Capsule(x, y, 67, 51, 58, 54, 1.1f));
        return Over(Fill(lenses, new Color(1, 1, 1, .16f)), Fill(rims, Hex("#0B1A1D")));
    }

    static Color Flower(float x, float y)
    {
        float petals = float.MaxValue;
        for (int i = 0; i < 5; i++)
        {
            float a = i * Mathf.PI * 2 / 5 + .3f;
            petals = Mathf.Min(petals, Circle(x, y, 31 + Mathf.Cos(a) * 4.6f, 80 + Mathf.Sin(a) * 4.6f, 3.6f));
        }
        float centre = Circle(x, y, 31, 80, 2.8f);
        return Over(Outlined(Union(petals, centre), Hex("#FFF2F6")), Fill(centre, Hex("#FFC93C")));
    }

    // Shape with the same dark outline as the body, so small extras stay readable.
    static Color Outlined(float distance, Color c) =>
        Over(Fill(distance - 2.5f, new Color(.016f, .043f, .055f, .85f)), Fill(distance, c));

    // ---------- shapes (signed distance: negative inside) ----------

    static float Circle(float x, float y, float cx, float cy, float r) => Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) - r;
    static float Ellipse(float x, float y, float cx, float cy, float rx, float ry)
    {
        float dx = (x - cx) / rx, dy = (y - cy) / ry;
        return (Mathf.Sqrt(dx * dx + dy * dy) - 1) * Mathf.Min(rx, ry);   // close enough for soft edges
    }
    static float Capsule(float x, float y, float ax, float ay, float bx, float by, float r)
    {
        var p = new Vector2(x - ax, y - ay); var b = new Vector2(bx - ax, by - ay);
        float t = Mathf.Clamp01(Vector2.Dot(p, b) / Vector2.Dot(b, b));
        return (p - b * t).magnitude - r;
    }
    static float RoundRect(float x, float y, float cx, float cy, float hx, float hy, float r)
    {
        float qx = Mathf.Abs(x - cx) - hx + r, qy = Mathf.Abs(y - cy) - hy + r;
        return new Vector2(Mathf.Max(qx, 0), Mathf.Max(qy, 0)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0) - r;
    }
    static float Triangle(float x, float y, Vector2 a, Vector2 b, Vector2 c)
    {
        var p = new Vector2(x, y);
        float d = Mathf.Min(Edge(p, a, b), Mathf.Min(Edge(p, b, c), Edge(p, c, a)));
        float s = Mathf.Sign(Cross(b - a, p - a)) + Mathf.Sign(Cross(c - b, p - b)) + Mathf.Sign(Cross(a - c, p - c));
        return Mathf.Abs(s) >= 3 ? -d : d;
    }
    static float Edge(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b - a; float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Vector2.Dot(ab, ab));
        return (p - (a + ab * t)).magnitude;
    }
    static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
    static float Union(params float[] d) { float m = float.MaxValue; foreach (var v in d) m = Mathf.Min(m, v); return m; }

    static readonly Color Clear = new Color(0, 0, 0, 0);
    static Color Fill(float distance, Color c) { c.a *= Mathf.Clamp01(.5f - distance * Unit); return c; }
    static Color Over(Color dst, Color src)
    {
        float a = src.a + dst.a * (1 - src.a);
        if (a <= 0) return Clear;
        return new Color((src.r * src.a + dst.r * dst.a * (1 - src.a)) / a, (src.g * src.a + dst.g * dst.a * (1 - src.a)) / a,
            (src.b * src.a + dst.b * dst.a * (1 - src.a)) / a, a);
    }
    static Color Hex(string s) { ColorUtility.TryParseHtmlString(s, out var c); return c; }

    // ---------- baking ----------

    static void Bake(string name, bool overwrite, Func<float, float, Color> shade)
    {
        string path = Folder + "/" + name + ".png";
        if (!overwrite && File.Exists(path)) return;     // keep hand edits
        var pixels = new Color[Size * Size];
        for (int py = 0; py < Size; py++)
            for (int px = 0; px < Size; px++)
            {
                Color sum = Clear; float alpha = 0;
                const int samples = 4;
                for (int s = 0; s < samples; s++)
                {
                    float ox = s % 2 == 0 ? .25f : .75f, oy = s < 2 ? .25f : .75f;
                    var c = shade((px + ox) / Unit, (py + oy) / Unit);
                    sum += new Color(c.r * c.a, c.g * c.a, c.b * c.a, c.a);   // premultiplied average
                    alpha += c.a;
                }
                sum /= samples;
                pixels[py * Size + px] = sum.a > 0 ? new Color(sum.r / sum.a, sum.g / sum.a, sum.b / sum.a, sum.a) : Clear;
            }
        var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
        texture.SetPixels(pixels); texture.Apply();
        File.WriteAllBytes(path, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true; importer.mipmapEnabled = false;
        importer.npotScale = TextureImporterNPOTScale.None; importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.wrapMode = TextureWrapMode.Clamp;
        var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect; importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
    }
}
