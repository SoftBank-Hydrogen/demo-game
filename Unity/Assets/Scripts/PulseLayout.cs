using System;
using UnityEngine;
using UnityEngine.UI;

// Two Inspector-editable layouts share the same objects and button listeners.
public class PulseLayout : MonoBehaviour
{
    [Serializable] public struct Pose
    {
        public Vector2 position, size;
        public int fontSize;
        public TextAnchor alignment;
        public void Apply(RectTransform rect, Text text)
        {
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            if (text != null) { text.fontSize = fontSize; text.alignment = alignment; }
        }
    }
    [Serializable] public class Item
    {
        public RectTransform rect;
        public Text text;
        public Pose landscape, portrait;
    }
    public CanvasScaler scaler;
    public RectTransform stage;
    public Vector2 landscapeSize = new Vector2(1440, 900);
    public Vector2 portraitSize = new Vector2(900, 1600);
    public float portraitAspect = 1.2f;
    public Item[] items;
    public bool IsPortrait { get; private set; }
    int width, height;

    public bool ApplyScreen()
    {
        if (width == Screen.width && height == Screen.height) return false;
        width = Screen.width; height = Screen.height;
        Apply((float)width / Mathf.Max(1, height) < portraitAspect);
        return true;
    }
    public void Apply(bool portrait)
    {
        IsPortrait = portrait;
        scaler.referenceResolution = stage.sizeDelta = portrait ? portraitSize : landscapeSize;
        foreach (var item in items) (portrait ? item.portrait : item.landscape).Apply(item.rect, item.text);
    }
    [ContextMenu("Preview landscape")] void PreviewLandscape() => Apply(false);
    [ContextMenu("Preview portrait")] void PreviewPortrait() => Apply(true);
    [ContextMenu("Save current positions to landscape")] void SaveLandscape() => Capture(false);
    [ContextMenu("Save current positions to portrait")] void SavePortrait() => Capture(true);
    void Capture(bool portrait)
    {
#if UNITY_EDITOR
        UnityEditor.Undo.RecordObject(this, "Save PULSE layout");
#endif
        foreach (var item in items)
        {
            var pose = new Pose { position = item.rect.anchoredPosition, size = item.rect.sizeDelta,
                fontSize = item.text != null ? item.text.fontSize : 0,
                alignment = item.text != null ? item.text.alignment : TextAnchor.MiddleLeft };
            if (portrait) item.portrait = pose; else item.landscape = pose;
        }
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }
}
