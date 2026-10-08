using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

// Explicit one-time migration. Normal builds only read the saved scene.
public class PulseSceneAuthoring
{
    Transform transform;
    CanvasScaler scaler;
    RectTransform stage;
    readonly Dictionary<string,RectTransform> nodes = new Dictionary<string,RectTransform>();
    readonly Dictionary<string,Text> texts = new Dictionary<string,Text>();
    readonly List<Image> pips = new List<Image>();
    PulseOrbView orb;
    Image tapArt, tapInner, progressBar;
    Button tapButton;
    Font regular, bold, mono;
    bool portrait;
    Color mint=Hex("#5CF2C4"), secondary=Hex("#8FA6A8"), bright=Hex("#EAF4F2");
    static Color Hex(string s) { ColorUtility.TryParseHtmlString(s,out var c); return c; }
    [MenuItem("Pulse/Migrate legacy scene UI (once)")]
    public static void Migrate()
    {
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/Pulse.unity");
        var view=UnityEngine.Object.FindFirstObjectByType<PulseView>();
        if(view.GetComponentInChildren<Canvas>() != null) {
            view.GetComponentInChildren<PulseOrbView>().FitToParent();
            EditorSceneManager.SaveScene(scene);
            Debug.Log("PULSE scene already authored; preserving edits."); return;
        }
        PulseArtwork.Generate();
        new PulseSceneAuthoring().Create(view);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("PULSE_UI_MIGRATED");
    }
    // Batch entry point for this migration; regular Web builds never recreate UI.
    public static void MigrateAndBuild() { Migrate(); PulseBuild.Web(); }
    void Create(PulseView view)
    {
        transform=view.transform;
        regular=AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/Fonts/Inter-Regular.ttf");
        bold=AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/Fonts/Inter-SemiBold.ttf");
        mono=AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/Fonts/RobotoMono-Regular.ttf");
        if(regular==null || bold==null || mono==null) throw new Exception("Missing PULSE fonts");
        BuildUI();
        var layout=view.gameObject.AddComponent<PulseLayout>(); layout.scaler=scaler; layout.stage=stage;
        var items=new List<PulseLayout.Item>();
        foreach(var pair in nodes) {
            if(pair.Key=="Background" || pair.Key=="Stage") continue;
            texts.TryGetValue(pair.Key,out var text);
            items.Add(new PulseLayout.Item {rect=pair.Value,text=text});
        }
        layout.items=items.ToArray();
        portrait=false; Layout(); Capture(layout,false);
        portrait=true; Layout(); Capture(layout,true);
        layout.Apply(false);
        orb.FitToParent();
        var so=new SerializedObject(view);
        Set(so,"api",view.GetComponent<PulseApi>()); Set(so,"layout",layout); Set(so,"orb",orb);
        Set(so,"tapArt",tapArt); Set(so,"tapInner",tapInner); Set(so,"progressBar",progressBar);
        Set(so,"tapButton",tapButton);
        foreach(var name in new[]{"Count","NextPulse","Pulses","Personal","Notice","TapWord","TapHint","Telemetry"})
            Set(so,char.ToLowerInvariant(name[0])+name.Substring(1)+"Text",texts[name]);
        var list=so.FindProperty("pips"); list.arraySize=pips.Count;
        for(int i=0;i<pips.Count;i++) list.GetArrayElementAtIndex(i).objectReferenceValue=pips[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }
    static void Capture(PulseLayout layout,bool portrait)
    {
        foreach(var item in layout.items) {
            var pose=new PulseLayout.Pose {position=item.rect.anchoredPosition,size=item.rect.sizeDelta,
                fontSize=item.text!=null?item.text.fontSize:0,alignment=item.text!=null?item.text.alignment:TextAnchor.MiddleLeft};
            if(portrait) item.portrait=pose; else item.landscape=pose;
        }
    }
    static void Set(SerializedObject so,string name,UnityEngine.Object value) => so.FindProperty(name).objectReferenceValue=value;
    static Image ChildImage(string name,Transform parent,string sprite,Color color,Vector2 size)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(Image)); go.transform.SetParent(parent,false);
        var image=go.GetComponent<Image>(); image.sprite=PulseArtwork.Load(sprite); image.color=color; image.raycastTarget=false;
        image.rectTransform.sizeDelta=size; return image;
    }
    PulseOrbView CreateOrb(RectTransform parent)
    {
        var root=new GameObject("Artwork",typeof(RectTransform)); root.transform.SetParent(parent,false);
        root.GetComponent<RectTransform>().sizeDelta=Vector2.one*660;
        var view=root.AddComponent<PulseOrbView>();
        ChildImage("Frame",root.transform,"Frame",Color.white,Vector2.one*660);
        var ticks=ChildImage("Ticks",root.transform,"Ticks",Color.white,Vector2.one*660);
        var history=new List<GameObject>();
        for(int i=0;i<5;i++) {
            Color c=mint;c.a=.18f-.02f*i;
            var ring=ChildImage("PulseRing"+(i+1),root.transform,"FineRing",c,Vector2.one*(504*(217+10*i)/250f));
            history.Add(ring.gameObject);ring.gameObject.SetActive(false);
        }
        ChildImage("Track",root.transform,"Track",Color.white,Vector2.one*376);
        var progress=ChildImage("Progress",root.transform,"Progress",Color.white,Vector2.one*376);
        progress.type=Image.Type.Filled;progress.fillMethod=Image.FillMethod.Radial360;progress.fillOrigin=(int)Image.Origin360.Top;
        progress.fillClockwise=true;progress.fillAmount=.002f;
        var tip=ChildImage("Tip",root.transform,"Tip",Color.white,Vector2.one*18);tip.gameObject.SetActive(false);
        var glow=ChildImage("Glow",root.transform,"Glow",mint,Vector2.one*544);
        var core=ChildImage("Core",root.transform,"Core",Color.white,Vector2.one*296);
        var wave=ChildImage("Wave",root.transform,"Wave",mint,Vector2.one*504);wave.gameObject.SetActive(false);
        var so=new SerializedObject(view);
        Set(so,"ticks",ticks.rectTransform);Set(so,"core",core.rectTransform);Set(so,"glow",glow.rectTransform);
        Set(so,"tip",tip.rectTransform);Set(so,"wave",wave.rectTransform);Set(so,"progress",progress);
        Set(so,"glowImage",glow);Set(so,"waveImage",wave);
        var list=so.FindProperty("history");list.arraySize=history.Count;
        for(int i=0;i<history.Count;i++) list.GetArrayElementAtIndex(i).objectReferenceValue=history[i];
        so.ApplyModifiedPropertiesWithoutUndo();
        view.Show(0,0,0,true);
        return view;
    }
    RectTransform Node(string name, Transform parent)
    {
        var node = new GameObject(name, typeof(RectTransform)); node.transform.SetParent(parent, false);
        var rect = node.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1);
        nodes[name] = rect; return rect;
    }
    Text Label(string name, string value, int size, bool strong = false, TextAnchor anchor = TextAnchor.MiddleLeft, Font face = null)
    {
        var r = Node(name, stage); var t = r.gameObject.AddComponent<Text>();
        t.font = face != null ? face : strong ? bold : regular; t.fontSize = size; t.text = value; t.alignment = anchor;
        t.color = secondary; t.raycastTarget = false; t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
        texts[name] = t; return t;
    }
    Image Art(string name, string sprite, Color color)
    {
        var r = Node(name, stage); var g = r.gameObject.AddComponent<Image>(); if (sprite != null) g.sprite = PulseArtwork.Load(sprite); if (sprite == "Rounded") g.type = Image.Type.Sliced; g.color = color; g.raycastTarget = false; return g;
    }
    void BuildUI()
    {
        var canvasObject = new GameObject("PULSE Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        scaler = canvasObject.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        var bg = Node("Background", canvasObject.transform);
        bg.anchorMin = Vector2.zero; bg.anchorMax = Vector2.one; bg.offsetMin = bg.offsetMax = Vector2.zero;
        bg.gameObject.AddComponent<PulseArt>();
        bg.GetComponent<PulseArt>().raycastTarget = false;
        stage = Node("Stage", canvasObject.transform); stage.anchorMin = stage.anchorMax = stage.pivot = new Vector2(.5f, .5f);
        Art("TopLine", null, Hex("#173035"));
        var orbRoot = Node("Orb", stage);
        orb = CreateOrb(orbRoot);
        Label("CountLabel", "TOTAL TAPS", 14, true);
        Label("Count", "—", 112, true).color = bright;
        Art("ProgressTrack", null, Hex("#164A40"));
        progressBar = Art("Progress", null, mint); progressBar.type = Image.Type.Filled; progressBar.fillMethod = Image.FillMethod.Horizontal; progressBar.fillAmount = 0;
        progressBar.sprite = PulseArtwork.Load("Solid");
        Label("NextPulse", "NEXT PULSE   0 / 50", 12, false, TextAnchor.MiddleLeft, mono);
        Label("Pulses", "PULSES  0", 12, false, TextAnchor.MiddleRight, mono).color = mint;
        for (int i = 0; i < 8; i++) pips.Add(Art("Pip" + i, null, Hex("#164A40")));
        tapArt = Art("TapButton", "Rounded", Hex("#2FB894")); tapArt.raycastTarget = true;
        tapInner = ChildImage("Fill", tapArt.transform, "Rounded", Hex("#102D2B"), Vector2.zero);
        tapInner.type = Image.Type.Sliced;
        tapInner.rectTransform.anchorMin=Vector2.zero; tapInner.rectTransform.anchorMax=Vector2.one;
        tapInner.rectTransform.offsetMin=Vector2.one*1.5f; tapInner.rectTransform.offsetMax=Vector2.one*-1.5f;
        tapButton = tapArt.gameObject.AddComponent<Button>(); tapButton.targetGraphic = tapArt; tapButton.transition = Selectable.Transition.None;
        tapButton.navigation = new Navigation { mode = Navigation.Mode.None };
        
        Label("TapWord", "TAP", 72, true, TextAnchor.MiddleCenter).color = bright;
        Label("TapHint", "Click · Space · Touch", 14, false, TextAnchor.MiddleCenter);
        Label("Personal", "YOU  0", 14, false, TextAnchor.MiddleCenter);
        Label("Notice", "", 15, false, TextAnchor.MiddleCenter);
        Art("BottomLine", null, Hex("#173035"));
        Label("Telemetry", "LAST TAP RTT  —     SHARED RATE  —     UNCONFIRMED  0", 12, false, TextAnchor.MiddleLeft, mono);
        if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() == null)
        {
            var events = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            // Space is handled once by the game, not again by UI Submit.
            events.GetComponent<StandaloneInputModule>().submitButton = "UnusedSubmit";
            events.GetComponent<StandaloneInputModule>().cancelButton = "UnusedCancel";
            events.GetComponent<EventSystem>().sendNavigationEvents = false;
        }

    }
    void Place(string name, float x, float y, float w, float h) { var r = nodes[name]; r.anchoredPosition = new Vector2(x, -y); r.sizeDelta = new Vector2(w, h); }
    void Layout()
    {

        float w = portrait ? 900 : 1440, h = portrait ? 1600 : 900;
        scaler.referenceResolution = new Vector2(w, h); stage.sizeDelta = new Vector2(w, h);
        if (!portrait)
        {
            Place("TopLine", 48, 78, 1344, 1);
            Place("Orb", 200, 140, 640, 640);
            Place("CountLabel", 960, 205, 400, 24); Place("Count", 949, 236, 440, 132);
            Place("ProgressTrack", 960, 393, 400, 3); Place("Progress", 960, 393, 400, 3); Place("NextPulse", 960, 407, 270, 24); Place("Pulses", 1230, 407, 130, 24);
            for (int i = 0; i < 8; i++) Place("Pip" + i, 1064 + i * 26, 460, 14, 6);
            Place("TapButton", 960, 488, 400, 185); Place("TapWord", 960, 510, 400, 95); Place("TapHint", 960, 618, 400, 30);
            Place("Personal", 960, 696, 400, 30); Place("Notice", 880, 742, 550, 44);
            Place("BottomLine", 48, 828, 1344, 1); Place("Telemetry", 48, 850, 1120, 26);
        }
        else
        {
            Place("TopLine", 48, 120, 804, 1);
            Place("Orb", 120, 150, 660, 660);
            Place("CountLabel", 0, 855, 900, 35); Place("Count", 0, 890, 900, 136);
            Place("ProgressTrack", 100, 1070, 700, 4); Place("Progress", 100, 1070, 700, 4); Place("NextPulse", 100, 1088, 500, 30); Place("Pulses", 580, 1088, 220, 30);
            for (int i = 0; i < 8; i++) Place("Pip" + i, 335 + i * 31, 1150, 16, 7);
            Place("TapButton", 100, 1180, 700, 190); Place("TapWord", 100, 1200, 700, 100); Place("TapHint", 100, 1320, 700, 32);
            Place("Personal", 0, 1400, 900, 34); Place("Notice", 48, 1450, 804, 42);
            Place("BottomLine", 48, 1520, 804, 1); Place("Telemetry", 48, 1540, 570, 30);
        }
        SetFont("Brand", 28, 40); SetFont("Connection", 13, 21);
        SetFont("CountLabel", 14, 22); SetFont("Count", 112, 122);
        SetFont("NextPulse", 12, 20); SetFont("Pulses", 12, 20); SetFont("TapWord", 72, 76);
        SetFont("TapHint", 14, 23); SetFont("Personal", 14, 22); SetFont("Notice", 15, 22); SetFont("Telemetry", 12, 20);
        foreach (var n in new[] { "CountLabel", "Count" }) texts[n].alignment = portrait ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft;
    }
    void SetFont(string name, int desktop, int mobile) { texts[name].fontSize = portrait ? mobile : desktop; }
}
