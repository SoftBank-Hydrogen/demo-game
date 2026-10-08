using UnityEngine;
using UnityEngine.UI;

// Drives the liquid shader: share of team A -> surface height, sudden changes -> bigger waves.
public class TugLiquidView : MonoBehaviour
{
    [SerializeField] Image liquid;              // Image with the UI/TugLiquid material and no sprite
    [SerializeField] RectTransform ticks;       // optional slowly rotating ring
    [SerializeField] Color teamA = new Color(.361f, .949f, .769f), teamB = new Color(1f, .71f, .278f);
    [SerializeField, Min(.1f)] float followSpeed = 6;
    [SerializeField] float restAmplitude = .012f, maxAmplitude = .09f, splashGain = 1.6f, settleSpeed = 1.4f;
    [SerializeField] float tickSpeed = 2f;

    Material material;
    float shownRatio = .5f, amplitude, phase;
    static readonly int LevelId = Shader.PropertyToID("_Level"), AmpId = Shader.PropertyToID("_Amp"),
        PhaseId = Shader.PropertyToID("_Phase"), ColorAId = Shader.PropertyToID("_ColorA"), ColorBId = Shader.PropertyToID("_ColorB");

    void Awake()
    {
        // Own copy so property changes don't touch the shared asset.
        material = new Material(liquid.material);
        liquid.material = material;
        material.SetColor(ColorAId, teamA);
        material.SetColor(ColorBId, teamB);
    }

    void OnDestroy() { if (material != null) Destroy(material); }

    // ratioA: 0..1 share of team A. Call every frame.
    public void Show(float ratioA, bool reducedMotion)
    {
        float dt = Time.unscaledDeltaTime;
        float before = shownRatio;
        shownRatio = reducedMotion ? ratioA : Mathf.Lerp(shownRatio, ratioA, 1 - Mathf.Exp(-dt * followSpeed));

        if (reducedMotion) amplitude = 0;
        else
        {
            amplitude += Mathf.Abs(shownRatio - before) * splashGain;
            amplitude = Mathf.Lerp(amplitude, restAmplitude, 1 - Mathf.Exp(-dt * settleSpeed));
            amplitude = Mathf.Min(amplitude, maxAmplitude);
            phase += dt;
        }
        material.SetFloat(LevelId, HeightForArea(shownRatio));
        material.SetFloat(AmpId, amplitude);
        material.SetFloat(PhaseId, phase);
        if (ticks != null && !reducedMotion) ticks.localRotation = Quaternion.Euler(0, 0, Time.unscaledTime * tickSpeed);
    }

    // Height (0..1 of the diameter) at which the filled part of a circle has the given area share,
    // so 30% of taps looks like 30% of the glass.
    static float HeightForArea(float share)
    {
        share = Mathf.Clamp01(share);
        float lo = -1, hi = 1;
        for (int i = 0; i < 24; i++)
        {
            float t = (lo + hi) * .5f;
            float area = (t * Mathf.Sqrt(1 - t * t) + Mathf.Asin(t)) / Mathf.PI + .5f;
            if (area < share) lo = t; else hi = t;
        }
        return ((lo + hi) * .5f + 1) * .5f;
    }
}
