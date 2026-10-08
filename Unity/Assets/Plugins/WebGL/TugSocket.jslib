// Browser WebSocket bridge for Unity Web.
// Events are queued here and polled by C# every frame (no callbacks into Unity).
var TugSocketLib = {
  $tug: { socket: null, queue: [] },

  TugSocketConnect: function (urlPtr) {
    var url = UTF8ToString(urlPtr);
    if (tug.socket) { try { tug.socket.close(); } catch (e) {} }
    tug.queue = [];
    var ws;
    try { ws = new WebSocket(url); } catch (e) { tug.queue.push('c1006'); return; }
    tug.socket = ws;
    ws.onopen = function () { if (tug.socket === ws) tug.queue.push('o'); };
    ws.onmessage = function (e) { if (tug.socket === ws && typeof e.data === 'string') tug.queue.push('m' + e.data); };
    ws.onclose = function (e) { if (tug.socket === ws) { tug.socket = null; tug.queue.push('c' + e.code); } };
    ws.onerror = function () {};
  },

  TugSocketSend: function (textPtr) {
    var ws = tug.socket;
    if (!ws || ws.readyState !== 1) return 0;
    ws.send(UTF8ToString(textPtr));
    return 1;
  },

  TugSocketClose: function () {
    if (tug.socket) { var ws = tug.socket; tug.socket = null; ws.close(); }
  },

  // Returns the next event ('o', 'm<json>', 'c<code>') or null when empty.
  TugSocketPoll: function () {
    if (!tug.queue.length) return null;
    var s = tug.queue.shift();
    var size = lengthBytesUTF8(s) + 1;
    var buffer = _malloc(size);
    stringToUTF8(s, buffer, size);
    return buffer;
  },

  // Reads a page setting such as window.GAME_SERVER. Empty string when missing.
  TugPageValue: function (keyPtr) {
    var value = window[UTF8ToString(keyPtr)];
    var s = value == null ? '' : String(value);
    var size = lengthBytesUTF8(s) + 1;
    var buffer = _malloc(size);
    stringToUTF8(s, buffer, size);
    return buffer;
  },

  // Test hook: copies client state to window.__tugState.
  TugReport: function (jsonPtr) {
    window.__tugState = JSON.parse(UTF8ToString(jsonPtr));
  }
};
autoAddDeps(TugSocketLib, '$tug');
mergeInto(LibraryManager.library, TugSocketLib);
