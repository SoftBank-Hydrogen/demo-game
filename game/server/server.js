'use strict';
const http = require('node:http');
const fs = require('node:fs');
const path = require('node:path');
const { randomUUID } = require('node:crypto');
const { WebSocketServer } = require('ws');
const { createRoom } = require('./room');
const { openScores } = require('./db');

const HOST = '127.0.0.1';
const PORT = 8080;
const ALLOWED_ORIGINS = ['http://localhost:3000', 'http://localhost:8080'];

const BROADCAST_MS = 50;            // 20 updates per second
const MAX_MESSAGE_BYTES = 512;
const MAX_MESSAGES_PER_SECOND = 30; // per connection
const KICK_AFTER_FLOOD_SECONDS = 3;

function createTugServer(options = {}) {
  const clock = options.clock || Date.now;
  const room = createRoom(options.room);
  const scores = options.scores !== undefined ? options.scores : openScores();
  const startedAt = clock();
  const clients = new Map();        // ws -> { id, joined, second, count, floodSeconds }
  const counters = { messages: 0, rejected: 0, perSecond: new Map() };

  const server = http.createServer((req, res) => {
    const url = new URL(req.url, 'http://localhost');
    if (req.method === 'GET' && url.pathname === '/stats') return sendJson(res, 200, stats());
    if (req.method === 'GET' && url.pathname === '/api/scoreboard') {
      if (!scores) return sendJson(res, 503, { error: 'scoreboard_unavailable' });
      return sendJson(res, 200, { ...scores.summary(), recent: scores.recentRounds(5) });
    }
    if (req.method === 'GET' && url.pathname === '/api/rounds') {
      if (!scores) return sendJson(res, 503, { error: 'scoreboard_unavailable' });
      return sendJson(res, 200, { rounds: scores.recentRounds(Number(url.searchParams.get('limit')) || 10) });
    }
    if (req.method === 'GET' && url.pathname === '/debug') {
      res.writeHead(200, { 'Content-Type': 'text/html; charset=utf-8' });
      return fs.createReadStream(path.join(__dirname, 'debug.html')).pipe(res);
    }
    sendJson(res, 404, { error: 'not_found' });
  });

  const wss = new WebSocketServer({ noServer: true, maxPayload: MAX_MESSAGE_BYTES });

  server.on('upgrade', (req, socket, head) => {
    const url = new URL(req.url, 'http://localhost');
    if (url.pathname !== '/ws') return reject(socket, 404);
    const origin = req.headers.origin;
    if (origin && !ALLOWED_ORIGINS.includes(origin)) return reject(socket, 403);
    wss.handleUpgrade(req, socket, head, ws => onConnect(ws));
  });

  // A connection becomes a player only after it sends {"type":"join"}.
  // Others (such as Sky's probe) are never counted, assigned a team or sent game state.
  function onConnect(ws) {
    const client = { id: randomUUID().slice(0, 8), joined: false, second: -1, count: 0, floodSeconds: 0 };
    clients.set(ws, client);
    ws.on('message', (data, isBinary) => onMessage(ws, client, data, isBinary));
    ws.on('close', () => {
      clients.delete(ws);
      if (client.joined) { room.leave(client.id); sendRoster(); }
    });
    ws.on('error', () => {});
  }

  function onMessage(ws, client, data, isBinary) {
    const now = clock();
    counters.messages++;
    const second = Math.floor(now / 1000);
    counters.perSecond.set(second, (counters.perSecond.get(second) || 0) + 1);

    // Per-connection rate limit. Excess messages are dropped; a client that keeps
    // flooding for several seconds is disconnected.
    if (client.second !== second) {
      if (client.count > MAX_MESSAGES_PER_SECOND) client.floodSeconds++;
      else client.floodSeconds = 0;
      client.second = second;
      client.count = 0;
    }
    client.count++;
    if (client.floodSeconds >= KICK_AFTER_FLOOD_SECONDS) return ws.close(1008, 'rate_limit');
    if (client.count > MAX_MESSAGES_PER_SECOND || isBinary) { counters.rejected++; return; }

    let msg;
    try { msg = JSON.parse(data.toString('utf8')); } catch { counters.rejected++; return; }
    if (!msg || typeof msg.type !== 'string') { counters.rejected++; return; }

    if (msg.type === 'sky.probe') {
      // Reply only to this connection, echoing the nonce. Does not join or affect the game.
      if (typeof msg.nonce !== 'string' || msg.nonce.length > 64) { counters.rejected++; return; }
      return send(ws, { type: 'sky.probe.ack', nonce: msg.nonce, serverTime: now });
    }
    if (msg.type === 'join' && !client.joined) {
      client.joined = true;
      const team = room.join(client.id);
      send(ws, { type: 'welcome', id: client.id, team, ...room.snapshot(now) });
      if (scores) send(ws, { type: 'scoreboard', ...scores.summary() });
      sendRoster();
      return;
    }
    if (msg.type === 'tap' && client.joined) {
      room.tap(client.id, msg.n ?? 1, now);
      return;
    }
    counters.rejected++;
  }

  function broadcast() {
    const now = clock();
    const result = room.tick(now);
    if (result) {
      everyone({ type: 'result', ...result });
      if (scores) {
        try {
          scores.saveRound(result);
          everyone({ type: 'scoreboard', ...scores.summary() });
        } catch (e) { console.error('Could not save round', e.message); }
      }
    }
    if (room.playerCount > 0) everyone({ type: 'state', ...room.snapshot(now) });
  }

  // Who is on which team, in join order. Sent only when it changes (join/leave), not at 20Hz.
  function sendRoster() {
    const roster = [];
    for (const c of clients.values()) if (c.joined) roster.push({ id: c.id, team: room.teamOf(c.id) });
    everyone({ type: 'roster', roster });
  }

  function everyone(message) {
    const text = JSON.stringify(message);
    for (const [ws, client] of clients) {
      if (client.joined && ws.readyState === ws.OPEN) ws.send(text);
    }
  }

  function stats() {
    const now = clock();
    const second = Math.floor(now / 1000);
    for (const key of counters.perSecond.keys()) if (key < second - 2) counters.perSecond.delete(key);
    let others = 0;                                   // connected but not joined (e.g. Sky probe)
    for (const c of clients.values()) if (!c.joined) others++;
    const snap = room.snapshot(now);
    return {
      uptimeSeconds: Math.floor((now - startedAt) / 1000),
      connections: { players: room.playerCount, others },
      teams: snap.players,
      phase: snap.phase,
      round: snap.round,
      tapsPerSecond: room.tapsLastSecond(now),
      messagesPerSecond: counters.perSecond.get(second - 1) || 0,
      totals: {
        tapsAccepted: room.stats.tapsAccepted,
        tapsLimited: room.stats.tapsLimited,
        tapsOutsideRound: room.stats.tapsOutsideRound,
        messages: counters.messages,
        messagesRejected: counters.rejected,
      },
    };
  }

  const timer = setInterval(broadcast, BROADCAST_MS);
  server.on('close', () => { clearInterval(timer); if (scores) scores.close(); });
  server.closeAll = () => {
    for (const ws of clients.keys()) ws.terminate();
    return new Promise(resolve => server.close(resolve));
  };
  server.room = room;
  return server;
}

function send(ws, message) {
  if (ws.readyState === ws.OPEN) ws.send(JSON.stringify(message));
}

function sendJson(res, status, data) {
  res.writeHead(status, { 'Content-Type': 'application/json; charset=utf-8', 'Cache-Control': 'no-store' });
  res.end(JSON.stringify(data));
}

function reject(socket, status) {
  socket.write(`HTTP/1.1 ${status} ${http.STATUS_CODES[status]}\r\nConnection: close\r\n\r\n`);
  socket.destroy();
}

if (require.main === module) {
  createTugServer().listen(PORT, HOST, () => console.log(`Tug server on http://${HOST}:${PORT}`));
}

module.exports = { createTugServer };
