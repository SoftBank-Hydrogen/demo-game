using System;
using System.Runtime.InteropServices;
#if !UNITY_WEBGL || UNITY_EDITOR
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
#endif

// Minimal text WebSocket. Browser builds use TugSocket.jslib; the Editor uses .NET ClientWebSocket.
// Both produce the same events, read with Poll() on the main thread.
public sealed class TugSocket
{
    public enum Kind { Open, Message, Close }
    public struct Event { public Kind kind; public string text; }

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] static extern void TugSocketConnect(string url);
    [DllImport("__Internal")] static extern int TugSocketSend(string text);
    [DllImport("__Internal")] static extern void TugSocketClose();
    [DllImport("__Internal")] static extern string TugSocketPoll();
    [DllImport("__Internal")] static extern string TugPageValue(string key);

    public static string PageValue(string key) => TugPageValue(key);
    public void Connect(string url) => TugSocketConnect(url);
    public bool Send(string text) => TugSocketSend(text) == 1;
    public void Close() => TugSocketClose();
    public bool Poll(out Event e)
    {
        string raw = TugSocketPoll();
        e = default;
        if (string.IsNullOrEmpty(raw)) return false;
        char type = raw[0];
        e.text = raw.Substring(1);
        e.kind = type == 'o' ? Kind.Open : type == 'm' ? Kind.Message : Kind.Close;
        return true;
    }
#else
    readonly ConcurrentQueue<Event> events = new ConcurrentQueue<Event>();
    ClientWebSocket socket;
    CancellationTokenSource cancel;

    public static string PageValue(string key) => "";

    public void Connect(string url)
    {
        Close();
        socket = new ClientWebSocket();
        cancel = new CancellationTokenSource();
        _ = Run(socket, new Uri(url), cancel.Token);
    }

    async Task Run(ClientWebSocket ws, Uri uri, CancellationToken token)
    {
        int code = 1006;
        try
        {
            await ws.ConnectAsync(uri, token);
            events.Enqueue(new Event { kind = Kind.Open, text = "" });
            var buffer = new byte[4096];
            var message = new StringBuilder();
            while (ws.State == WebSocketState.Open && !token.IsCancellationRequested)
            {
                var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), token);
                if (result.MessageType == WebSocketMessageType.Close) { code = (int)(result.CloseStatus ?? WebSocketCloseStatus.Empty); break; }
                message.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                if (result.EndOfMessage)
                {
                    events.Enqueue(new Event { kind = Kind.Message, text = message.ToString() });
                    message.Clear();
                }
            }
        }
        catch (Exception) { }
        if (!token.IsCancellationRequested) events.Enqueue(new Event { kind = Kind.Close, text = code.ToString() });
    }

    public bool Send(string text)
    {
        if (socket == null || socket.State != WebSocketState.Open) return false;
        var bytes = Encoding.UTF8.GetBytes(text);
        // ClientWebSocket allows one pending send; small messages complete immediately on localhost.
        try { socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancel.Token).Wait(200); }
        catch (Exception) { return false; }
        return true;
    }

    public void Close()
    {
        if (socket == null) return;
        cancel.Cancel();
        try { socket.Abort(); } catch (Exception) { }
        socket.Dispose();
        socket = null;
    }

    public bool Poll(out Event e) => events.TryDequeue(out e);
#endif
}
