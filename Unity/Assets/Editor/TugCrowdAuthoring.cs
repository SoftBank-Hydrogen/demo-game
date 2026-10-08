using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// One-time upgrade of the saved Tug scene: adds the character arena (rope, knot, Mochi template,
// "YOU" marker) and its layout poses. Does nothing if the arena already exists, so later edits stay.
public static class TugCrowdAuthoring
{
    [MenuItem("Tug/Add character arena (once)")]
    public static void Ensure()
    {
        var existing = Object.FindFirstObjectByType<TugCrowdView>(FindObjectsInactive.Include);
        if (existing != null) { UseSingleImage(existing); AddAccessories(existing); return; }
        var view = Object.FindFirstObjectByType<TugView>();
        var layout = view.GetComponent<PulseLayout>();
        var client = view.GetComponent<TugClient>();
        TugArtwork.Generate();   // creates only missing images
        var bold = AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/Fonts/Inter-SemiBold.ttf");

        var arena = Node("Arena", layout.stage);
        var rope = Img("Rope", arena, PulseArtwork.Load("Solid"), Hex("#C9B48A"));
        rope.rectTransform.pivot = new Vector2(0, .5f);
        rope.rectTransform.sizeDelta = new Vector2(100, 6);
        var knot = Img("Knot", arena, PulseArtwork.Load("Tip"), Color.white);
        knot.rectTransform.pivot = new Vector2(.5f, .5f);
        knot.rectTransform.sizeDelta = new Vector2(30, 30);

        var crowd = Node("Crowd", arena);
        crowd.anchorMin = Vector2.zero; crowd.anchorMax = Vector2.one; crowd.offsetMin = crowd.offsetMax = Vector2.zero;
        crowd.pivot = new Vector2(0, 1);

        var overflowA = Label("OverflowA", arena, bold, Hex("#5CF2C4"));
        var overflowB = Label("OverflowB", arena, bold, Hex("#FFB547"));

        // Character template (inactive; TugCrowdView clones it per player).
        var template = Node("MochiTemplate", arena);
        template.pivot = new Vector2(.5f, 0);
        template.sizeDelta = new Vector2(100, 100);
        AddMochiImage(template);
        var marker = Node("Marker", template);
        marker.anchorMin = marker.anchorMax = new Vector2(.5f, 1); marker.pivot = new Vector2(.5f, 0);
        marker.anchoredPosition = new Vector2(0, -6); marker.sizeDelta = new Vector2(80, 56);
        var arrow = Img("Arrow", marker, TugArtwork.Load("Marker"), Color.white);
        arrow.rectTransform.anchorMin = arrow.rectTransform.anchorMax = new Vector2(.5f, 0);
        arrow.rectTransform.pivot = new Vector2(.5f, 0);
        arrow.rectTransform.sizeDelta = new Vector2(30, 30);
        var you = Label("You", marker, bold, Hex("#EAF4F2"));
        you.text = "YOU"; you.fontSize = 15; you.alignment = TextAnchor.LowerCenter;
        you.rectTransform.anchorMin = you.rectTransform.anchorMax = new Vector2(.5f, 0);
        you.rectTransform.pivot = new Vector2(.5f, 0);
        you.rectTransform.anchoredPosition = new Vector2(0, 26); you.rectTransform.sizeDelta = new Vector2(80, 22);
        template.gameObject.SetActive(false);

        var crowdView = arena.gameObject.AddComponent<TugCrowdView>();
        Set(crowdView, ("client", client), ("arena", arena), ("crowd", crowd), ("template", template),
            ("rope", rope.rectTransform), ("knot", knot.rectTransform), ("overflowA", overflowA), ("overflowB", overflowB),
            ("teamASprite", TugArtwork.Load("MochiA")), ("teamBSprite", TugArtwork.Load("MochiB")));
        Set(view, ("crowd", crowdView));

        // Layout: arena under the orb; landscape orb moves up, portrait countdown goes inside the orb.
        var items = new List<PulseLayout.Item>(layout.items);
        items.Add(new PulseLayout.Item {
            rect = arena,
            landscape = new PulseLayout.Pose { position = new Vector2(40, -690), size = new Vector2(880, 130) },
            portrait = new PulseLayout.Pose { position = new Vector2(40, -866), size = new Vector2(820, 205) } });
        foreach (var item in items)
        {
            if (item.rect.name == "Orb") item.landscape.position = new Vector2(180, -40);
            if (item.rect.name == "Big") item.portrait.position = new Vector2(0, -480);
        }
        layout.items = items.ToArray();
        layout.Apply(false);
        EditorSceneManager.MarkSceneDirty(view.gameObject.scene);
        EditorSceneManager.SaveScene(view.gameObject.scene);
        AddAccessories(crowdView);
        Debug.Log("TUG_ARENA_ADDED");
    }

    // Upgrade for scenes made with layered sprites (Shadow/Outline/Body/Ears/Face): replace them with
    // one complete image per team, referenced from the scene.
    static void UseSingleImage(TugCrowdView view)
    {
        var template = view.transform.Find("MochiTemplate");
        if (template == null || template.Find("Mochi") != null) return;
        TugArtwork.Generate();
        foreach (var old in new[] { "Shadow", "Outline", "Body", "Ears", "Face" })
        {
            var child = template.Find(old);
            if (child != null) Object.DestroyImmediate(child.gameObject);
        }
        AddMochiImage(template);
        Set(view, ("teamASprite", TugArtwork.Load("MochiA")), ("teamBSprite", TugArtwork.Load("MochiB")));
        EditorSceneManager.MarkSceneDirty(view.gameObject.scene);
        EditorSceneManager.SaveScene(view.gameObject.scene);
        Debug.Log("TUG_SINGLE_IMAGE");
    }

    // Upgrade: an accessory image above the body and the list of accessory sprites
    // (TugCrowdView picks one per player).
    static void AddAccessories(TugCrowdView view)
    {
        var template = view.transform.Find("MochiTemplate");
        if (template == null || template.Find("Accessory") != null) return;
        TugArtwork.Generate();   // creates only missing images
        var sprites = TugArtwork.LoadAccessories();
        var img = FullImage("Accessory", template, sprites[0]);
        img.transform.SetSiblingIndex(template.Find("Mochi").GetSiblingIndex() + 1);

        var so = new SerializedObject(view);
        var list = so.FindProperty("accessories");
        list.arraySize = sprites.Length;
        for (int i = 0; i < sprites.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(view.gameObject.scene);
        EditorSceneManager.SaveScene(view.gameObject.scene);
        Debug.Log("TUG_ACCESSORIES_ADDED");
    }

    // Full-size image behind the marker; TugCrowdView sets the team sprite per player.
    static void AddMochiImage(Transform template) => FullImage("Mochi", template, TugArtwork.Load("MochiA")).transform.SetAsFirstSibling();

    static Image FullImage(string name, Transform parent, Sprite sprite)
    {
        var img = Img(name, parent, sprite, Color.white);
        img.rectTransform.anchorMin = Vector2.zero; img.rectTransform.anchorMax = Vector2.one;
        img.rectTransform.offsetMin = img.rectTransform.offsetMax = Vector2.zero;
        return img;
    }
    static RectTransform Node(string name, Transform parent)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1);
        return rect;
    }

    static Image Img(string name, Transform parent, Sprite sprite, Color color)
    {
        var image = Node(name, parent).gameObject.AddComponent<Image>();
        image.sprite = sprite; image.color = color; image.raycastTarget = false;
        return image;
    }

    static Text Label(string name, Transform parent, Font font, Color color)
    {
        var t = Node(name, parent).gameObject.AddComponent<Text>();
        t.font = font; t.fontSize = 22; t.color = color; t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
        t.rectTransform.pivot = new Vector2(0, 0); t.rectTransform.sizeDelta = new Vector2(60, 30);
        return t;
    }

    static void Set(Object target, params (string name, Object value)[] refs)
    {
        var so = new SerializedObject(target);
        foreach (var (name, value) in refs) so.FindProperty(name).objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static Color Hex(string s) { ColorUtility.TryParseHtmlString(s, out var c); return c; }
}
