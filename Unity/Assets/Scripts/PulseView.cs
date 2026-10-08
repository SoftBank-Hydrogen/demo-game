using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.UI;

// Screen controller: reads PulseApi state, writes it to the scene's UI and
// forwards player input (click / touch / Space / Enter) to PulseApi.
// It never changes the score by itself; only server replies do that.
public class PulseView : MonoBehaviour
{
    const float BrowserReportInterval = .5f;

    [Header("Scene references")]
    [SerializeField] PulseApi api;
    [SerializeField] PulseLayout layout;
    [SerializeField] PulseOrbView orb;
    [SerializeField] Image tapArt;         // Outer button image (ready/disabled colour).
    [SerializeField] Image tapInner;       // Inner fill that flashes when pressed.
    [SerializeField] Image progressBar;    // Horizontal bar under the counter.
    [SerializeField] Image[] pips;         // One dot per in-flight request.
    [SerializeField] Button tapButton;
    [SerializeField] Text countText, nextPulseText, pulsesText, personalText;
    [SerializeField] Text noticeText, tapWordText, tapHintText, telemetryText;

    [Header("Game and animation")]
    [SerializeField, Min(1)] int tapsPerPulse = 50;
    [SerializeField, Min(.01f)] float pressFadeSpeed = 5;
    [SerializeField, Min(.01f)] float pulseFadeSpeed = 1.2f;
    [SerializeField, Min(.01f)] float chargeSpeed = 10;

    [Header("Palette")]
    [SerializeField] Color mint = Hex("#5CF2C4"), secondary = Hex("#8FA6A8");
    [SerializeField] Color warning = Hex("#FFB547"), error = Hex("#FF6B5A"), uncertain = Hex("#D9B887");
    [SerializeField] Color idlePip = Hex("#235047"), buttonReady = Hex("#2FB894"), buttonDisabled = Hex("#304644");
    [SerializeField] Color buttonInside = Hex("#102D2B"), buttonPressedInside = Hex("#563515");
    [SerializeField] Vector2 tapFontSizes = new Vector2(72, 76);      // x = landscape, y = portrait
    [SerializeField] Vector2 offlineFontSizes = new Vector2(43, 55);  // x = landscape, y = portrait

    // Follows the browser/OS "reduce motion" setting; there is no in-game toggle.
    bool reducedMotion;

    // Animation state (0..1 values that fade out over time).
    float press;           // Button flash after a tap.
    float pulseFlash;      // Wave after every completed pulse.
    float smoothProgress;  // Displayed progress, eased towards the real value.

    // Used to detect a newly completed pulse or a new server session.
    int previousTotal;
    string lastEpoch;
    float nextReportTime;

    static Color Hex(string s) { ColorUtility.TryParseHtmlString(s, out var c); return c; }

#if UNITY_WEBGL && !UNITY_EDITOR
    // Implemented in Assets/Plugins/WebGL/PulseBridge.jslib.
    [DllImport("__Internal")] static extern void PulseReport(string json);
    [DllImport("__Internal")] static extern int PulseReducedMotion();
#endif

    // ---------- Lifecycle ----------

    void Awake()
    {
        Application.targetFrameRate = 60;
        tapsPerPulse = Mathf.Max(1, tapsPerPulse);
#if UNITY_WEBGL && !UNITY_EDITOR
        reducedMotion = PulseReducedMotion() == 1;
#endif
        layout.ApplyScreen();
        tapButton.onClick.AddListener(Tap);
        api.Changed += Refresh;   // Redraw text whenever network state changes.
        Refresh();
    }

    void OnDestroy()
    {
        if (api != null) api.Changed -= Refresh;
        if (tapButton != null) tapButton.onClick.RemoveListener(Tap);
    }

    public void Tap()
    {
        if (!api.CanTap) return;
        press = 1;
        api.Tap();
    }

    // ---------- Text and state (runs only when something changed) ----------

    void Refresh()
    {
        bool hasState = !string.IsNullOrEmpty(api.Epoch);
        bool offline = api.Started && !api.Connected;

        countText.text = hasState ? api.Total.ToString("N0") : "—";

        nextPulseText.text = "NEXT PULSE   " + (api.Total % tapsPerPulse) + " / " + tapsPerPulse;
        pulsesText.text = "PULSES  " + api.Total / tapsPerPulse;
        personalText.text = "YOU  " + api.Confirmed + (api.Pending > 0 ? " · " + api.Pending + " sending" : "");

        noticeText.text = api.Notice;
        noticeText.color = offline ? error : api.Unconfirmed > 0 ? uncertain : secondary;

        RefreshTapButton(offline);
        telemetryText.text = TelemetryLine(hasState);

        for (int i = 0; i < pips.Length; i++)
            pips[i].color = i < api.Pending ? warning : idlePip;

        DetectPulseAndSession();
        Report();
    }

    void RefreshTapButton(bool offline)
    {
        tapWordText.text = offline ? "OFFLINE" : "TAP";
        Vector2 sizes = offline ? offlineFontSizes : tapFontSizes;
        tapWordText.fontSize = (int)(layout.IsPortrait ? sizes.y : sizes.x);

        if (!api.Connected) tapHintText.text = "Waiting for the shared session";
        else if (api.Pending >= PulseApi.MaxPending) tapHintText.text = "Sending... waiting for server";
        else tapHintText.text = "Click · Space · Touch";

        tapButton.interactable = api.CanTap;
    }

    string TelemetryLine(bool hasState)
    {
        string rtt = api.Confirmed > 0 ? api.LastLatency + "ms" : "—";
        string rate = hasState ? api.Rate + "/s" : "—";
        string unconfirmed = api.Unconfirmed > 0 ? "     ? " + api.Unconfirmed : "";
        return "RTT  " + rtt + "     RATE  " + rate + unconfirmed;
    }

    void DetectPulseAndSession()
    {
        if (lastEpoch != api.Epoch)
        {
            // New server session: jump straight to its progress, no celebration wave.
            lastEpoch = api.Epoch;
            previousTotal = api.Total;
            smoothProgress = CurrentProgress();
        }
        if (api.Total / tapsPerPulse > previousTotal / tapsPerPulse) pulseFlash = 1;
        previousTotal = api.Total;
    }

    float CurrentProgress() => api.Total % tapsPerPulse / (float)tapsPerPulse;

    // ---------- Per-frame input and animation ----------

    void Update()
    {
        if (layout.ApplyScreen()) Refresh();   // Orientation changed: re-apply font sizes.
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)) Tap();

        float dt = Time.unscaledDeltaTime;
        press = Mathf.Max(0, press - dt * pressFadeSpeed);
        pulseFlash = Mathf.Max(0, pulseFlash - dt * pulseFadeSpeed);

        float target = CurrentProgress();
        smoothProgress = reducedMotion ? target : Mathf.Lerp(smoothProgress, target, 1 - Mathf.Exp(-dt * chargeSpeed));
        if (Mathf.Abs(smoothProgress - target) < .0001f) smoothProgress = target;

        orb.Show(smoothProgress, api.Total / tapsPerPulse, pulseFlash, reducedMotion);
        // Image setters mark geometry dirty only when their value actually changes.
        tapArt.color = api.CanTap ? Color.Lerp(buttonReady, warning, press) : buttonDisabled;
        tapInner.color = Color.Lerp(buttonInside, buttonPressedInside, press);
        progressBar.fillAmount = smoothProgress;

        if (Time.unscaledTime >= nextReportTime)
        {
            Report();
            nextReportTime = Time.unscaledTime + BrowserReportInterval;
        }
    }

    // ---------- Browser test hook (window.__pulseState) ----------

    [Serializable] class BrowserState
    {
        public bool ready, connected, reducedMotion;
        public string epoch;
        public int total, confirmed, pending, unconfirmed, lastLatency, rate;
    }

    void Report()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        PulseReport(JsonUtility.ToJson(new BrowserState {
            ready = true, connected = api.Connected, reducedMotion = reducedMotion, epoch = api.Epoch,
            total = api.Total, confirmed = api.Confirmed, pending = api.Pending,
            unconfirmed = api.Unconfirmed, lastLatency = api.LastLatency, rate = api.Rate }));
#endif
    }
}
