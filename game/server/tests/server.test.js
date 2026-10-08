'use strict';
const { test } = require('node:test');
const assert = require('node:assert/strict');
const WebSocket = require('ws');
const { createTugServer } = require('../server');

async function start(room, scores = null) {
  const server = createTugServer({ room, scores });
  await new Promise(r => server.listen(0, '127.0.0.1', r));
  return { server, base: `127.0.0.1:${server.address().port}` };
}

function connect(base, { path = '/ws', origin } = {}) {
  const ws = new WebSocket(`ws://${base}${path}`, origin ? { origin } : {});
  const inbox = [];
  const waiters = [];
  ws.on('message', data => {
    const m = JSON.parse(data);
    const waiter = waiters.find(w => w.match(m));
    if (waiter) { waiters.splice(waiters.indexOf(waiter), 1); waiter.resolve(m); }   // consumed
    else inbox.push(m);
  });
  ws.next = (match, timeout = 3000) => {
    const found = inbox.find(match);
    if (found) { inbox.splice(inbox.indexOf(found), 1); return Promise.resolve(found); }
    return new Promise((resolve, rejectP) => {
      const w = { match, resolve };
      waiters.push(w);
      setTimeout(() => rejectP(new Error('timeout waiting for message')), timeout);
    });
  };
  ws.ready = new Promise((resolve, rejectP) => { ws.once('open', resolve); ws.once('error', rejectP); ws.once('unexpected-response', (_, res) => rejectP(new Error('HTTP ' + res.statusCode))); });
  return ws;
}

const fastRoom = { countdownMs: 100, roundMs: 600, resultMs: 200, random: () => 0.1 };

test('two clients join opposite teams and see each other\'s taps; round result is broadcast', async () => {
  const { server, base } = await start(fastRoom);
  try {
    const a = connect(base); const b = connect(base);
    await Promise.all([a.ready, b.ready]);
    a.send(JSON.stringify({ type: 'join' })); b.send(JSON.stringify({ type: 'join' }));
    const wa = await a.next(m => m.type === 'welcome');
    const wb = await b.next(m => m.type === 'welcome');
    assert.notEqual(wa.team, wb.team);

    await a.next(m => m.type === 'state' && m.phase === 'playing');
    a.send(JSON.stringify({ type: 'tap', n: 4 }));
    b.send(JSON.stringify({ type: 'tap', n: 1 }));
    // b sees a's taps (and its own) through the 20Hz state broadcast
    const seen = await b.next(m => m.type === 'state' && m.taps[wa.team] === 4 && m.taps[wb.team] === 1);
    assert.equal(seen.phase, 'playing');

    const result = await a.next(m => m.type === 'result', 3000);
    assert.equal(result.winner, wa.team);
    a.close(); b.close();
  } finally { await server.closeAll(); }
});

test('sky.probe is answered without join, echoes the nonce only to the sender and never counts as a player', async () => {
  const { server, base } = await start(fastRoom);
  try {
    const player = connect(base);
    await player.ready;
    player.send(JSON.stringify({ type: 'join' }));
    await player.next(m => m.type === 'welcome');

    const probe = connect(base);                       // plain /ws, no join (as Sky does)
    await probe.ready;
    const nonce = '9f2c4e1a7b3d5f60a1b2c3d4e5f60718';
    probe.send(JSON.stringify({ type: 'sky.probe', nonce }));
    const ack = await probe.next(m => m.type === 'sky.probe.ack', 5000);
    assert.equal(ack.nonce, nonce);

    await new Promise(r => setTimeout(r, 200));
    const stats = await (await fetch(`http://${base}/stats`)).json();
    assert.deepEqual(stats.connections, { players: 1, others: 1 });
    assert.deepEqual(stats.teams, { A: 1, B: 0 });     // probe has no team
    assert.equal(stats.phase, 'waiting');              // one player only: the probe did not start a round
    await assert.rejects(player.next(m => m.type === 'sky.probe.ack', 300));   // not broadcast
    await assert.rejects(probe.next(m => m.type === 'state' || m.type === 'welcome', 300)); // no game data

    probe.send(JSON.stringify({ type: 'sky.probe', nonce: 'x'.repeat(65) }));   // too long: ignored
    probe.send(JSON.stringify({ type: 'sky.probe', nonce: 42 }));               // not a string: ignored
    await assert.rejects(probe.next(m => m.type === 'sky.probe.ack', 300));
    probe.close(); player.close();
  } finally { await server.closeAll(); }
});
test('roster lists joined players with teams, updates on leave, and never includes probe connections', async () => {
  const { server, base } = await start(fastRoom);
  try {
    const a = connect(base); const b = connect(base); const probe = connect(base);
    await Promise.all([a.ready, b.ready, probe.ready]);
    a.send(JSON.stringify({ type: 'join' }));
    const wa = await a.next(m => m.type === 'welcome');
    b.send(JSON.stringify({ type: 'join' }));
    const wb = await b.next(m => m.type === 'welcome');
    const full = await a.next(m => m.type === 'roster' && m.roster.length === 2);
    assert.deepEqual(full.roster, [{ id: wa.id, team: wa.team }, { id: wb.id, team: wb.team }]);
    probe.send(JSON.stringify({ type: 'sky.probe', nonce: 'n' }));
    await probe.next(m => m.type === 'sky.probe.ack');
    await assert.rejects(probe.next(m => m.type === 'roster', 300));
    b.close();
    const after = await a.next(m => m.type === 'roster' && m.roster.length === 1);
    assert.equal(after.roster[0].id, wa.id);
    a.close(); probe.close();
  } finally { await server.closeAll(); }
});

test('origins outside the allowed list are rejected; listed origins are accepted', async () => {
  const { server, base } = await start(fastRoom);
  try {
    const bad = connect(base, { origin: 'https://game.example.com' });
    await assert.rejects(bad.ready, /403/);
    const good = connect(base, { origin: 'http://localhost:3000' });
    await good.ready;
    good.close();
  } finally { await server.closeAll(); }
});

test('oversized messages close the connection; malformed messages are counted as rejected', async () => {
  const { server, base } = await start(fastRoom);
  try {
    const ws = connect(base);
    await ws.ready;
    ws.send('not json');
    ws.send(JSON.stringify({ type: 'unknown' }));
    await new Promise(r => setTimeout(r, 100));
    let stats = await (await fetch(`http://${base}/stats`)).json();
    assert.equal(stats.totals.messagesRejected, 2);

    const closed = new Promise(r => ws.once('close', code => r(code)));
    ws.send('x'.repeat(2000));
    assert.equal(await closed, 1009);  // message too big
  } finally { await server.closeAll(); }
});

test('messages beyond 30 per second on one connection are dropped', async () => {
  const { server, base } = await start(fastRoom);
  try {
    const ws = connect(base);
    await ws.ready;
    // Align to the start of a second so the burst stays inside one rate window.
    await new Promise(r => setTimeout(r, 1000 - (Date.now() % 1000) + 20));
    for (let i = 0; i < 50; i++) ws.send(JSON.stringify({ type: 'sky.probe', nonce: String(i) }));
    await new Promise(r => setTimeout(r, 300));
    const stats = await (await fetch(`http://${base}/stats`)).json();
    assert.equal(stats.totals.messagesRejected, 20);
    ws.close();
  } finally { await server.closeAll(); }
});

test('there is no /health endpoint yet (intentionally not deployment-ready)', async () => {
  const { server, base } = await start(fastRoom);
  try {
    assert.equal((await fetch(`http://${base}/health`)).status, 404);
    assert.equal((await fetch(`http://${base}/stats`)).status, 200);
  } finally { await server.closeAll(); }
});
