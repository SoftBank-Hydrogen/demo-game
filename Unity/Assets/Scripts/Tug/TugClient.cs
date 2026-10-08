using System;
using UnityEngine;

[Serializable] public class TeamCounts { public int A; public int B; }
[Serializable] public class RosterEntry { public string id; public string team; }

// Every server message has this shape; unused fields stay at their defaults.
[Serializable] public class TugMessage
{
    public string type, id, team, phase, winner, nonce;
    public int round;
    public long remainingMs, serverTime, startedAt, endedAt;
    public TeamCounts taps = new TeamCounts(), players = new TeamCounts();
    // scoreboard message
    public int rounds, draws, totalTaps, bestRoundTaps;
    public TeamCounts wins = new TeamCounts();
    // roster message (join order)
    public RosterEntry[] roster;
}

// Talks to the game server over WebSocket and keeps the latest server state.
// The server decides teams, time and taps; this class only sends tap counts.
public class TugClient : MonoBehaviour
{
    const string DefaultServer = "ws://localhost:8080/ws";
    const float SendInterval = .05f;     // batch taps, at most 20 messages per second
    const int MaxTapsPerMessage = 20;
    const float PingInterval = 2f;
    static readonly float[] RetryDelays = { 1, 2, 4, 8 };

    public event Action Changed;
    public event Action<TugMessage> RoundFinished;

    // Connection
    public string ServerUrl { get; private set; }
    public string Target { get; private set; }
    public string Release { get; private set; }
    public bool Connected { get; private set; }
    public bool EverConnected { get; private set; }
    public int RetryInSeconds { get; private set; }
    public int PingMs { get; private set; } = -1;

    // Game state from the server
    public string PlayerId { get; private set; }
    public string Team { get; private set; }
    public string Phase { get; private set; } = "connecting";
    public int Round { get; private set; }
    public TeamCounts Taps { get; private set; } = new TeamCounts();
    public TeamCounts Players { get; private set; } = new TeamCounts();
    public TugMessage LastResult { get; private set; }
    public TugMessage Scoreboard { get; private set; }  // saved rounds (server SQLite); null if unavailable
    public RosterEntry[] Roster { get; private set; } = new RosterEntry[0];
    public int RosterVersion { get; private set; }       // increases whenever the roster changes
    public int MyTaps { get; private set; }   // taps I sent this round (not all may be counted)

    public float RemainingSeconds =>
        Mathf.Max(0, (remainingMs - (Time.realtimeSinceStartup - receivedAt) * 1000f) / 1000f);
    public bool CanTap => Connected && Phase == "playing";

    readonly TugSocket socket = new TugSocket();
    long remainingMs;
    float receivedAt, nextSend, nextPing, reconnectAt = -1;
    int pendingTaps, attempt;

    void Start()
    {
        // The page may define these before Unity starts (index.html or Sky's config.json loader).
        ServerUrl = Or(TugSocket.PageValue("GAME_SERVER"), DefaultServer);
        Target = Or(TugSocket.PageValue("GAME_TARGET"), "local");
        Release = Or(TugSocket.PageValue("GAME_RELEASE"), "dev");
        Connect();
    }

    static string Or(string value, string fallback) => string.IsNullOrEmpty(value) ? fallback : value;

    void Connect()
    {
        reconnectAt = -1;
        RetryInSeconds = 0;
        socket.Connect(ServerUrl);
    }

    void OnDestroy() => socket.Close();

    public void Tap()
    {
        if (!CanTap) return;
        pendingTaps++;
        MyTaps++;
    }

    void Update()
    {
        while (socket.Poll(out var e)) Handle(e);
        float now = Time.realtimeSinceStartup;

        if (reconnectAt > 0)
        {
            int left = Mathf.CeilToInt(reconnectAt - now);
            if (left != RetryInSeconds) { RetryInSeconds = left; Changed?.Invoke(); }
            if (now >= reconnectAt) Connect();
            return;
        }
        if (!Connected) return;

        if (pendingTaps > 0 && now >= nextSend)
        {
            int n = Mathf.Min(pendingTaps, MaxTapsPerMessage);
            if (socket.Send("{\"type\":\"tap\",\"n\":" + n + "}")) pendingTaps -= n;
            nextSend = now + SendInterval;
        }
        if (now >= nextPing)
        {
            // The server's sky.probe reply doubles as a round-trip time measurement.
            socket.Send("{\"type\":\"sky.probe\",\"nonce\":\"" + Mathf.RoundToInt(now * 1000) + "\"}");
            nextPing = now + PingInterval;
        }
    }

    void Handle(TugSocket.Event e)
    {
        switch (e.kind)
        {
            case TugSocket.Kind.Open:
                Connected = true;
                EverConnected = true;
                socket.Send("{\"type\":\"join\"}");   // become a player (probe-only connections never join)
                attempt = 0;
                nextPing = 0;
                Changed?.Invoke();
                break;
            case TugSocket.Kind.Close:
                Connected = false;
                Phase = "offline";
                pendingTaps = 0;
                Roster = new RosterEntry[0];       // everyone disappears until we rejoin
                RosterVersion++;
                float delay = RetryDelays[Mathf.Min(attempt, RetryDelays.Length - 1)];
                attempt++;
                reconnectAt = Time.realtimeSinceStartup + delay;
                RetryInSeconds = Mathf.CeilToInt(delay);
                Changed?.Invoke();
                break;
            case TugSocket.Kind.Message:
                TugMessage m;
                try { m = JsonUtility.FromJson<TugMessage>(e.text); } catch (Exception) { return; }
                if (m != null) Apply(m);
                break;
        }
    }

    void Apply(TugMessage m)
    {
        switch (m.type)
        {
            case "welcome":
                PlayerId = m.id;
                Team = m.team;
                ApplyState(m);
                break;
            case "state":
                ApplyState(m);
                break;
            case "result":
                LastResult = m;
                RoundFinished?.Invoke(m);
                break;
            case "scoreboard":
                Scoreboard = m;
                break;
            case "roster":
                Roster = m.roster ?? new RosterEntry[0];
                RosterVersion++;
                break;
            case "sky.probe.ack":
                if (int.TryParse(m.nonce, out int sentMs))
                    PingMs = Mathf.Max(0, Mathf.RoundToInt(Time.realtimeSinceStartup * 1000) - sentMs);
                break;
            default:
                return;
        }
        Changed?.Invoke();
    }

    void ApplyState(TugMessage m)
    {
        if (m.round != Round && m.phase == "playing") MyTaps = 0;   // a new round started
        Phase = m.phase;
        Round = m.round;
        Taps = m.taps ?? new TeamCounts();
        Players = m.players ?? new TeamCounts();
        remainingMs = m.remainingMs;
        receivedAt = Time.realtimeSinceStartup;
    }
}
