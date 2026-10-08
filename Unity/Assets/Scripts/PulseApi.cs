using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

// JSON shape returned by the Node server (/api/state and /api/tap).
[Serializable] public class PulseSnapshot
{
    public string epoch;               // Server session ID; changes when the server restarts.
    public int total;                  // Shared score of everyone.
    public int processedLastSecond;    // Taps the server accepted in the previous full second.
    public bool duplicate;             // True when this request ID was already counted.
    public string error;
}
[Serializable] class TapRequest { public string id; public string epoch; }

// All network communication. Holds only server-confirmed numbers;
// the score never increases locally before the server replies.
public class PulseApi : MonoBehaviour
{
    public const int MaxPending = 8;          // In-flight tap requests allowed per client.
    const int RequestTimeoutSeconds = 5;
    const float PollIntervalSeconds = 1;
    const float NoticeSeconds = 6;
    const string EditorOrigin = "http://127.0.0.1:3000";

    public event Action Changed;              // PulseView redraws when this fires.

    public string Epoch { get; private set; }
    public int Total { get; private set; }
    public int Rate { get; private set; }
    public int Confirmed { get; private set; }    // My taps the server accepted.
    public int Unconfirmed { get; private set; }  // My taps whose result is unknown/failed.
    public int Pending { get; private set; }      // My taps waiting for a reply.
    public int LastLatency { get; private set; }  // Round-trip time of my last success, in ms.
    public bool Connected { get; private set; }
    public bool Started { get; private set; }     // At least one poll has finished.
    public string Notice { get; private set; } = "";
    public bool CanTap => Connected && Pending < MaxPending && !string.IsNullOrEmpty(Epoch);

    string origin;
    float noticeUntil;

    void Start()
    {
        // In the browser, talk to the server that served this page (same origin).
        // In the Unity Editor there is no page URL, so use the local server.
        origin = EditorOrigin;
        if (Uri.TryCreate(Application.absoluteURL, UriKind.Absolute, out var uri) && uri.Scheme.StartsWith("http"))
            origin = uri.GetLeftPart(UriPartial.Authority);
        StartCoroutine(PollLoop());
    }

    void Notify() => Changed?.Invoke();
    void SetNotice(string value) { Notice = value; noticeUntil = Time.unscaledTime + NoticeSeconds; }

    // ---------- Reading other players' progress: GET /api/state every second ----------

    IEnumerator PollLoop()
    {
        while (true)
        {
            using (var request = UnityWebRequest.Get(origin + "/api/state"))
            {
                request.timeout = RequestTimeoutSeconds;
                yield return request.SendWebRequest();
                Started = true;
                HandlePollResult(request);
                Notify();
            }
            // The next poll starts one second after this one finished, so polls never overlap.
            yield return new WaitForSecondsRealtime(PollIntervalSeconds);
        }
    }

    void HandlePollResult(UnityWebRequest request)
    {
        if (request.result != UnityWebRequest.Result.Success)
        {
            Connected = false;
            Notice = "Connection lost. Checking again automatically...";
            return;
        }
        PulseSnapshot data = Parse(request.downloadHandler.text);
        if (data == null)
        {
            Connected = false;
            Notice = "The server returned an unexpected response.";
            return;
        }
        bool recovering = !Connected && Epoch != null;
        Apply(data, allowNewSession: true);
        Connected = true;
        if (recovering) SetNotice("Back online.");
        else if (Time.unscaledTime >= noticeUntil) Notice = "";
    }

    static PulseSnapshot Parse(string json)
    {
        try
        {
            var data = JsonUtility.FromJson<PulseSnapshot>(json);
            return string.IsNullOrEmpty(data.epoch) ? null : data;
        }
        catch { return null; }
    }

    // Copies a server snapshot into local state.
    // Only polls may switch to a new session; a late tap reply from an old session is ignored.
    void Apply(PulseSnapshot data, bool allowNewSession)
    {
        if (data.epoch != Epoch)
        {
            if (!allowNewSession) return;
            bool hadSession = Epoch != null;
            Epoch = data.epoch;
            Total = data.total;
            Confirmed = 0;
            if (hadSession) SetNotice("A new server session has started. Shared energy reset.");
        }
        else
        {
            // Replies can arrive out of order; an older, smaller total must not shrink the score.
            Total = Mathf.Max(Total, data.total);
        }
        Rate = data.processedLastSecond;
    }

    // ---------- Sending my tap: POST /api/tap ----------

    public void Tap() { if (CanTap) StartCoroutine(SendTap()); }

    IEnumerator SendTap()
    {
        string session = Epoch;
        // A unique ID lets the server ignore the same request if it arrives twice.
        var payload = new TapRequest { id = Guid.NewGuid().ToString("N"), epoch = session };
        Pending++;
        Notify();
        float begin = Time.realtimeSinceStartup;

        using (var request = new UnityWebRequest(origin + "/api/tap", "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(payload)));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = RequestTimeoutSeconds;
            yield return request.SendWebRequest();

            Pending--;
            if (request.result == UnityWebRequest.Result.Success) HandleTapSuccess(request, session, begin);
            else HandleTapFailure(request.responseCode);
            Notify();
        }
    }

    void HandleTapSuccess(UnityWebRequest request, string session, float begin)
    {
        var data = Parse(request.downloadHandler.text);
        bool sameSession = data != null && data.epoch == Epoch && session == Epoch;
        if (!sameSession)
        {
            Unconfirmed++;
            SetNotice("Session changed. This tap belongs to the previous session.");
            return;
        }
        Apply(data, allowNewSession: false);
        Confirmed++;
        LastLatency = Mathf.RoundToInt((Time.realtimeSinceStartup - begin) * 1000);
    }

    void HandleTapFailure(long status)
    {
        Unconfirmed++;
        if (status == 409) SetNotice("Session changed. Synchronizing the new score...");
        else if (status == 429) SetNotice("A little too fast. Give the server a moment.");
        else SetNotice("A tap wasn't confirmed. It may have reached the server.");
        // Never replay a POST automatically: a lost response is not proof of a lost tap.
    }
}
