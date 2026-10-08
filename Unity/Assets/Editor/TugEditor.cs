using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Tug scene authoring (one time) and Web build. Normal builds only read the saved scene,
// so edits made in the Editor are kept.
public class TugEditor
{
    const string ScenePath = "Assets/Scenes/Tug.unity";
    const string MaterialPath = "Assets/Materials/TugLiquid.mat";

    [MenuItem("Tug/Create or open scene")]
    public static void CreateOrOpenScene()
    {
        if (File.Exists(ScenePath))
        {
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != ScenePath)
            {
                if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    throw new OperationCanceledException("Scene switch canceled.");
                EditorSceneManager.OpenScene(ScenePath);
            }
            TugCrowdAuthoring.Ensure();   // adds the character arena once to older saved scenes
            return;
        }
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var camera = new GameObject("Camera", typeof(Camera)).GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.016f, .027f, .043f);
        camera.orthographic = true;
        camera.transform.position = new Vector3(0, 0, -10);
        var root = new GameObject("Tug", typeof(TugClient), typeof(TugView));
        new TugEditor().Author(root);
        EditorSceneManager.SaveScene(scene, ScenePath);
        TugCrowdAuthoring.Ensure();
        AssetDatabase.SaveAssets();
        Debug.Log("TUG_SCENE_CREATED");
    }

    [MenuItem("Tug/Build Web")]
    public static void Web()
    {
        CreateOrOpenScene();
        var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (active.isDirty && !EditorSceneManager.SaveScene(active)) throw new Exception("Could not save the Tug scene.");

        // Remember shared settings so the PULSE build is not affected.
        string template = PlayerSettings.WebGL.template, product = PlayerSettings.productName;
        bool fallback = PlayerSettings.WebGL.decompressionFallback;
        try
        {
            PlayerSettings.productName = "PULSE TUG";
            PlayerSettings.WebGL.template = "PROJECT:Tug";
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            // Lets any static server (plain Nginx, S3) serve the files without Content-Encoding rules.
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.dataCaching = false;
            PlayerSettings.WebGL.initialMemorySize = 128;
            PlayerSettings.runInBackground = true;
            PlayerSettings.colorSpace = ColorSpace.Gamma;
            PlayerSettings.stripEngineCode = true;
            PlayerSettings.SplashScreen.show = false;

            var output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../game/client"));
            if (Directory.Exists(Path.Combine(output, "Build"))) Directory.Delete(Path.Combine(output, "Build"), true);
            Directory.CreateDirectory(output);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { ScenePath }, locationPathName = output, target = BuildTarget.WebGL, options = BuildOptions.None });
            Debug.Log("TUG_BUILD_RESULT " + report.summary.result + " bytes=" + report.summary.totalSize + " path=" + output);
            if (report.summary.result != BuildResult.Succeeded) throw new Exception("Tug Web build failed: " + report.summary.result);
        }
        finally
        {
            PlayerSettings.WebGL.template = template;
            PlayerSettings.productName = product;
            PlayerSettings.WebGL.decompressionFallback = fallback;
            AssetDatabase.SaveAssets();
        }
    }

    // ---------- one-time scene authoring ----------

    readonly Dictionary<string, RectTransform> nodes = new Dictionary<string, RectTransform>();
    readonly Dictionary<string, Text> texts = new Dictionary<string, Text>();
    RectTransform stage;
    CanvasScaler scaler;
    Font regular, bold, mono;
    bool portrait;
    static readonly Color mint = Hex("#5CF2C4"), amber = Hex("#FFB547"), secondary = Hex("#8FA6A8"), bright = Hex("#EAF4F2");
    static Color Hex(string s) { ColorUtility.TryParseHtmlString(s, out var c); return c; }

    void Author(GameObject root)
    {
        regular = AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/Fonts/Inter-Regular.ttf");
        bold = AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/Fonts/Inter-SemiBold.ttf");
        mono = AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/Fonts/RobotoMono-Regular.ttf");
        if (regular == null || bold == null || mono == null) throw new Exception("Missing fonts");
        if (PulseArtwork.Load("Frame") == null) PulseArtwork.Generate();

        var canvasObject = new GameObject("TUG Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(root.transform, false);
        canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        var bg = Node("Background", canvasObject.transform);
        bg.anchorMin = Vector2.zero; bg.anchorMax = Vector2.one; bg.offsetMin = bg.offsetMax = Vector2.zero;
        bg.gameObject.AddComponent<PulseArt>().raycastTarget = false;
        stage = Node("Stage", canvasObject.transform);
        stage.anchorMin = stage.anchorMax = stage.pivot = new Vector2(.5f, .5f);

        // Orb with the liquid
        var orb = Node("Orb", stage);
        var glow = Child("Glow", orb, PulseArtwork.Load("Glow"), new Color(1, 1, 1, .10f), 600);
        Child("Frame", orb, PulseArtwork.Load("Frame"), Color.white, 640);
        var ticks = Child("Ticks", orb, PulseArtwork.Load("Ticks"), Color.white, 640);
        var liquidImage = Child("Liquid", orb, null, Color.white, 400);
        liquidImage.material = LiquidMaterial();
        var liquid = orb.gameObject.AddComponent<TugLiquidView>();
        SetRefs(liquid, ("liquid", liquidImage), ("ticks", ticks.rectTransform));
        glow.transform.SetAsFirstSibling();

        Label("Phase", "CONNECTING", 22, true, TextAnchor.MiddleCenter).color = bright;
        Label("Big", "", 120, true, TextAnchor.MiddleCenter).color = bright;
        Label("TeamA", "TEAM A   0\n0 players", 18, true, TextAnchor.MiddleLeft).color = mint;
        Label("TeamB", "TEAM B   0\n0 players", 18, true, TextAnchor.MiddleRight).color = amber;

        var tapArt = Art("TapButton", PulseArtwork.Load("Rounded"), Hex("#304644"));
        tapArt.type = Image.Type.Sliced; tapArt.raycastTarget = true;
        var tapInner = Child("Fill", tapArt.rectTransform, PulseArtwork.Load("Rounded"), Hex("#102D2B"), 0);
        tapInner.type = Image.Type.Sliced;
        tapInner.rectTransform.anchorMin = Vector2.zero; tapInner.rectTransform.anchorMax = Vector2.one;
        tapInner.rectTransform.offsetMin = Vector2.one * 2; tapInner.rectTransform.offsetMax = Vector2.one * -2;
        var tapArea = tapArt.gameObject.AddComponent<TugTapArea>();
        SetRefs(tapArea, ("view", root.GetComponent<TugView>()));

        Label("TapWord", "WAIT", 72, true, TextAnchor.MiddleCenter).color = bright;
        Label("TapHint", "", 15, false, TextAnchor.MiddleCenter);
        Label("You", "", 16, true, TextAnchor.MiddleCenter);
        Art("BottomLine", null, Hex("#173035"));
        Label("Status", "●  CONNECTING", 13, true, TextAnchor.MiddleLeft, mono);
        Label("Badge", "", 12, false, TextAnchor.MiddleRight, mono);

        if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() == null)
        {
            var events = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            events.GetComponent<StandaloneInputModule>().submitButton = "UnusedSubmit";   // Space is handled by the game
            events.GetComponent<StandaloneInputModule>().cancelButton = "UnusedCancel";
            events.GetComponent<EventSystem>().sendNavigationEvents = false;
        }

        var layout = root.AddComponent<PulseLayout>();
        layout.scaler = scaler; layout.stage = stage;
        var items = new List<PulseLayout.Item>();
        foreach (var pair in nodes)
        {
            if (pair.Key == "Background" || pair.Key == "Stage") continue;
            texts.TryGetValue(pair.Key, out var text);
            items.Add(new PulseLayout.Item { rect = pair.Value, text = text });
        }
        layout.items = items.ToArray();
        portrait = false; Place(); Capture(layout, false);
        portrait = true; Place(); Capture(layout, true);
        layout.Apply(true);

        var view = root.GetComponent<TugView>();
        SetRefs(view, ("client", root.GetComponent<TugClient>()), ("layout", layout), ("liquid", liquid),
            ("tapArt", tapArt), ("tapInner", tapInner),
            ("teamAText", texts["TeamA"]), ("teamBText", texts["TeamB"]), ("phaseText", texts["Phase"]),
            ("bigText", texts["Big"]), ("youText", texts["You"]), ("tapWordText", texts["TapWord"]),
            ("tapHintText", texts["TapHint"]), ("statusText", texts["Status"]), ("badgeText", texts["Badge"]));
    }

    static Material LiquidMaterial()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material != null) return material;
        Directory.CreateDirectory("Assets/Materials");
        material = new Material(Shader.Find("UI/TugLiquid"));
        AssetDatabase.CreateAsset(material, MaterialPath);
        return material;
    }

    static void SetRefs(UnityEngine.Object target, params (string name, UnityEngine.Object value)[] refs)
    {
        var so = new SerializedObject(target);
        foreach (var (name, value) in refs) so.FindProperty(name).objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    RectTransform Node(string name, Transform parent)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1);
        nodes[name] = rect;
        return rect;
    }

    static Image Child(string name, RectTransform parent, Sprite sprite, Color color, float size)
    {
        var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        image.rectTransform.SetParent(parent, false);
        image.rectTransform.anchorMin = image.rectTransform.anchorMax = new Vector2(.5f, .5f);
        image.rectTransform.sizeDelta = Vector2.one * size;
        image.sprite = sprite; image.color = color; image.raycastTarget = false;
        return image;
    }

    Text Label(string name, string value, int size, bool strong, TextAnchor anchor, Font face = null)
    {
        var t = Node(name, stage).gameObject.AddComponent<Text>();
        t.font = face != null ? face : strong ? bold : regular;
        t.fontSize = size; t.text = value; t.alignment = anchor; t.color = secondary; t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
        texts[name] = t;
        return t;
    }

    Image Art(string name, Sprite sprite, Color color)
    {
        var g = Node(name, stage).gameObject.AddComponent<Image>();
        g.sprite = sprite; g.color = color; g.raycastTarget = false;
        return g;
    }

    void Set(string name, float x, float y, float w, float h) { var r = nodes[name]; r.anchoredPosition = new Vector2(x, -y); r.sizeDelta = new Vector2(w, h); }
    void SetFont(string name, int landscape, int portraitSize) => texts[name].fontSize = portrait ? portraitSize : landscape;

    void Place()
    {
        float w = portrait ? 900 : 1440, h = portrait ? 1600 : 900;
        scaler.referenceResolution = stage.sizeDelta = new Vector2(w, h);
        if (!portrait)
        {
            Set("Orb", 180, 110, 640, 640);
            Set("Phase", 960, 150, 400, 40);
            Set("Big", 960, 195, 400, 150);
            Set("TeamA", 960, 380, 200, 60); Set("TeamB", 1160, 380, 200, 60);
            Set("TapButton", 960, 470, 400, 200); Set("TapWord", 960, 495, 400, 100); Set("TapHint", 960, 610, 400, 30);
            Set("You", 960, 700, 400, 30);
            Set("BottomLine", 48, 828, 1344, 1);
            Set("Status", 48, 848, 500, 30); Set("Badge", 760, 848, 632, 30);
        }
        else
        {
            Set("Phase", 0, 70, 900, 50);
            Set("TeamA", 60, 140, 380, 70); Set("TeamB", 460, 140, 380, 70);
            Set("Orb", 130, 230, 640, 640);
            Set("Big", 0, 890, 900, 140);
            Set("TapButton", 100, 1080, 700, 240); Set("TapWord", 100, 1115, 700, 120); Set("TapHint", 100, 1245, 700, 40);
            Set("You", 0, 1350, 900, 40);
            Set("BottomLine", 48, 1500, 804, 1);
            Set("Status", 48, 1520, 400, 34); Set("Badge", 420, 1520, 432, 34);
        }
        SetFont("Phase", 22, 30); SetFont("Big", 120, 130); SetFont("TeamA", 18, 24); SetFont("TeamB", 18, 24);
        SetFont("TapWord", 72, 84); SetFont("TapHint", 15, 22); SetFont("You", 16, 24); SetFont("Status", 13, 20); SetFont("Badge", 12, 18);
    }

    static void Capture(PulseLayout layout, bool portrait)
    {
        foreach (var item in layout.items)
        {
            var pose = new PulseLayout.Pose { position = item.rect.anchoredPosition, size = item.rect.sizeDelta,
                fontSize = item.text != null ? item.text.fontSize : 0,
                alignment = item.text != null ? item.text.alignment : TextAnchor.MiddleLeft };
            if (portrait) item.portrait = pose; else item.landscape = pose;
        }
    }
}
