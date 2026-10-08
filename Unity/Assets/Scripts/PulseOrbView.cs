using UnityEngine;
using UnityEngine.UI;

// All artwork is saved in the scene. Only transforms, fill and visibility animate.
[ExecuteAlways]
public class PulseOrbView : MonoBehaviour
{
    [SerializeField] RectTransform ticks, core, glow, tip, wave;
    [SerializeField] Image progress, glowImage, waveImage;
    [SerializeField] GameObject[] history;
    [SerializeField] float tickSpeed = 2.00535f, breathSpeed = 1.7f, breathAmount = .012f;
    [SerializeField] Color mint = new Color(.361f, .949f, .769f);
    int lastPulses = -1;
    float lastValue = -1;
    bool lastReduced;

    public void Show(float value, int pulses, float flash, bool reduced)
    {
        if (reduced && lastReduced && value == lastValue && pulses == lastPulses) return;
        lastValue = value;
        lastReduced = reduced;
        progress.fillAmount = Mathf.Max(.002f, value);
        // This root has a fixed 660-unit coordinate space, scaled by its parent.
        ticks.localRotation = Quaternion.Euler(0, 0, reduced ? 0 : Time.unscaledTime * tickSpeed);
        float breath = reduced ? 1 : 1 + Mathf.Sin(Time.unscaledTime * breathSpeed) * breathAmount;
        float radius = (78 + 68 * (1 - (1 - value) * (1 - value))) * breath;
        core.localScale = Vector3.one * (radius / 146);
        glow.localScale = Vector3.one * (radius / 146);
        var glowColor = mint; glowColor.a = .19f + value * .08f; glowImage.color = glowColor;
        tip.gameObject.SetActive(value > .003f);
        if (value > .003f)
        {
            float angle = Mathf.PI / 2 - value * Mathf.PI * 2;
            tip.anchoredPosition = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 186;
        }
        bool showWave = flash > 0 && !reduced;
        wave.gameObject.SetActive(showWave);
        if (showWave)
        {
            wave.localScale = Vector3.one * ((190 + (1 - flash) * 110) / 250);
            var c = mint; c.a = flash * .65f; waveImage.color = c;
        }
        if (lastPulses != pulses)
        {
            for (int i = 0; i < history.Length; i++) history[i].SetActive(i < pulses);
            lastPulses = pulses;
        }
    }
    public void FitToParent()
    {
        // Parent fits either layout; sprites use the same local coordinate system.
        if (transform.parent is RectTransform parent)
        {
            float scale = Mathf.Min(parent.rect.width, parent.rect.height) / 660;
            if (transform.localScale.x != scale) transform.localScale = Vector3.one * scale;
        }
    }
    void OnRectTransformDimensionsChange() => FitToParent();
    void OnEnable() => FitToParent();
    void LateUpdate() => FitToParent();
}
