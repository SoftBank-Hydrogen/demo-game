using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.UI;

// Screen controller for the team tap battle. Reads TugClient, writes the scene's UI.
public class TugView : MonoBehaviour
{
    [Header("Scene references")]
    [SerializeField] TugClient client;
    [SerializeField] PulseLayout layout;
    [SerializeField] TugLiquidView liquid;
    [SerializeField] TugCrowdView crowd;
    [SerializeField] Image tapArt, tapInner;
    [SerializeField] Text teamAText, teamBText, phaseText, bigText, youText, tapWordText, tapHintText, statusText, badgeText;

    [Header("Palette")]
    [SerializeField] Color teamA = Hex("#5CF2C4"), teamB = Hex("#FFB547");
    [SerializeField] Color bright = Hex("#EAF4F2"), secondary = Hex("#8FA6A8"), error = Hex("#FF6B5A");
    [SerializeField] Color disabled = Hex("#304644"), inside = Hex("#102D2B");
    [SerializeField, Min(.01f)] float pressFadeSpeed = 6;

    bool reducedMotion;
    float press, nextReport;

    static Color Hex(string s) { ColorUtility.TryParseHtmlString(s, out var c); return c; }

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] static extern int PulseReducedMotion();
    [DllImport("__Internal")] static extern void TugReport(string json);
#endif

    void Awake()
    {
        Application.targetFrameRate = 60;
#if UNITY_WEBGL && !UNITY_EDITOR
        reducedMotion = PulseReducedMotion() == 1;
#endif
        layout.ApplyScreen();
        if (crowd != null) crowd.SetReducedMotion(reducedMotion);
        client.Changed += Refresh;
    }

    void Start() => Refresh();

    void OnDestroy() { if (client != null) client.Changed -= Refresh; }

    public void Tap()
    {
        if (!client.CanTap) return;
        press = 1;
        client.Tap();
    }

    Color TeamColor(string team) => team == "A" ? teamA : team == "B" ? teamB : secondary;

    void Refresh()
    {
        var taps = client.Taps;
        var players = client.Players;
        teamAText.text = "TEAM A   " + taps.A + "\n" + Count(players.A);
        teamBText.text = "TEAM B   " + taps.B + "\n" + Count(players.B);

        youText.text = client.Team == null ? "" : "YOU  ·  TEAM " + client.Team;
        youText.color = TeamColor(client.Team);

        switch (client.Phase)
        {
            case "waiting":   phaseText.text = "WAITING FOR PLAYERS"; break;
            case "countdown": phaseText.text = "GET READY"; break;
            case "playing":   phaseText.text = "TAP!"; break;
            case "result":    phaseText.text = ResultLine(); break;
            case "offline":   phaseText.text = "RECONNECTING"; break;
            default:          phaseText.text = "CONNECTING"; break;
        }
        phaseText.color = client.Phase == "result" && client.LastResult != null ? TeamColor(client.LastResult.winner) : bright;

        tapWordText.text = client.CanTap ? "TAP" : client.Connected ? "WAIT" : "OFFLINE";
        var board = client.Scoreboard;
        string wins = board == null ? "" : "   ·   WINS  A " + board.wins.A + " : " + board.wins.B + " B";
        tapHintText.text = client.CanTap ? "Tap fast · Space"
            : client.Connected ? "Next round soon" + wins
            : "Reconnecting in " + client.RetryInSeconds + "s";

        statusText.text = client.Connected
            ? "●  LIVE" + (client.PingMs >= 0 ? "   " + client.PingMs + " ms" : "")
            : client.EverConnected ? "●  RECONNECTING" : "●  CONNECTING";
        statusText.color = client.Connected ? teamA : client.EverConnected ? error : secondary;
        // Deployment badge: where this build thinks it is running (filled from the page settings).
        if (client.ServerUrl != null)
            badgeText.text = client.Target.ToUpperInvariant() + "  ·  " + client.Release + "  ·  " + HostOf(client.ServerUrl);
    }

    static string Count(int players) => players == 1 ? "1 player" : players + " players";

    string ResultLine()
    {
        var r = client.LastResult;
        if (r == null) return "ROUND OVER";
        string head = r.winner == "DRAW" ? "DRAW" : "TEAM " + r.winner + " WINS";
        return head + "   " + r.taps.A + " : " + r.taps.B;
    }

    static string HostOf(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var u) ? u.Authority : url;

    void Update()
    {
        if (layout.ApplyScreen()) Refresh();
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)) Tap();

        var taps = client.Taps;
        int total = taps.A + taps.B;
        liquid.Show(total > 0 ? taps.A / (float)total : .5f, reducedMotion);

        // Big centre number: seconds left in countdown/round, nothing otherwise.
        bool timed = client.Phase == "countdown" || client.Phase == "playing";
        bigText.text = timed ? Mathf.CeilToInt(client.RemainingSeconds).ToString() : "";

        press = Mathf.Max(0, press - Time.unscaledDeltaTime * pressFadeSpeed);
        var mine = TeamColor(client.Team);
        tapArt.color = client.CanTap ? Color.Lerp(mine, bright, press * .6f) : disabled;
        tapInner.color = Color.Lerp(inside, mine * .55f, press);

        if (Time.unscaledTime >= nextReport) { Report(); nextReport = Time.unscaledTime + .5f; }
    }

    [Serializable] class BrowserState
    {
        public bool connected, canTap, reducedMotion;
        public string team, phase, target, release, server;
        public int round, tapsA, tapsB, playersA, playersB, myTaps, pingMs, savedRounds = -1, winsA, winsB, shownA, shownB;
        public bool mineMarked;
    }

    void Report()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        var board = client.Scoreboard;
        TugReport(JsonUtility.ToJson(new BrowserState {
            shownA = crowd != null ? crowd.Shown("A") : 0, shownB = crowd != null ? crowd.Shown("B") : 0, mineMarked = crowd != null && crowd.MineMarked(),
            savedRounds = board != null ? board.rounds : -1, winsA = board != null ? board.wins.A : 0, winsB = board != null ? board.wins.B : 0,
            connected = client.Connected, canTap = client.CanTap, reducedMotion = reducedMotion,
            team = client.Team, phase = client.Phase, target = client.Target, release = client.Release, server = client.ServerUrl,
            round = client.Round, tapsA = client.Taps.A, tapsB = client.Taps.B,
            playersA = client.Players.A, playersB = client.Players.B, myTaps = client.MyTaps, pingMs = client.PingMs }));
#endif
    }
}
