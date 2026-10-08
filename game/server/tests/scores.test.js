'use strict';
const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const WebSocket = require('ws');
const { openScores, available } = require('../db');
const { createTugServer } = require('../server');

const skip = available() ? false : 'node:sqlite needs Node.js 22 or newer';
const round = (winner, a, b, at) => ({ winner, taps: { A: a, B: b }, players: { A: 2, B: 1 }, startedAt: at, endedAt: at + 30000 });

test('rounds are stored and summarised', { skip }, () => {
  const scores = openScores(':memory:');
  scores.saveRound(round('A', 40, 30, 1000));
  scores.saveRound(round('B', 10, 25, 2000));
  scores.saveRound(round('DRAW', 5, 5, 3000));
  assert.deepEqual(scores.summary(), { rounds: 3, wins: { A: 1, B: 1 }, draws: 1, totalTaps: 115, bestRoundTaps: 70 });
  const recent = scores.recentRounds(2);
  assert.equal(recent.length, 2);
  assert.equal(recent[0].winner, 'DRAW');          // newest first
  assert.deepEqual(recent[1].taps, { A: 10, B: 25 });
  scores.close();
});

test('an empty scoreboard returns zeros', { skip }, () => {
  const scores = openScores(':memory:');
  assert.deepEqual(scores.summary(), { rounds: 0, wins: { A: 0, B: 0 }, draws: 0, totalTaps: 0, bestRoundTaps: 0 });
  scores.close();
});

test('scores survive reopening the same file (but only on this disk)', { skip }, () => {
  const file = path.join(fs.mkdtempSync(path.join(os.tmpdir(), 'tug-')), 'scores.db');
  let scores = openScores(file);
  scores.saveRound(round('A', 3, 1, 1000));
  scores.close();
  scores = openScores(file);
  assert.equal(scores.summary().rounds, 1);
  scores.close();
});

test('a finished round is saved, broadcast as a scoreboard message and served over HTTP', { skip }, async () => {
  const server = createTugServer({ room: { countdownMs: 50, roundMs: 300, resultMs: 100, random: () => 0.1 }, scores: openScores(':memory:') });
  await new Promise(r => server.listen(0, '127.0.0.1', r));
  const base = `127.0.0.1:${server.address().port}`;
  try {
    const messages = [];
    const clients = [0, 1].map(() => new WebSocket(`ws://${base}/ws`));
    clients[0].on('message', d => messages.push(JSON.parse(d)));
    await Promise.all(clients.map(ws => new Promise(r => ws.once('open', r))));
    clients.forEach(ws => ws.send(JSON.stringify({ type: 'join' })));
    await new Promise(r => setTimeout(r, 150));          // countdown done, round playing
    clients[0].send(JSON.stringify({ type: 'tap', n: 4 }));
    const deadline = Date.now() + 3000;
    while (!messages.some(m => m.type === 'scoreboard' && m.rounds === 1) && Date.now() < deadline) await new Promise(r => setTimeout(r, 50));
    const board = messages.find(m => m.type === 'scoreboard' && m.rounds === 1);
    assert.ok(board, 'scoreboard broadcast after the round');
    assert.equal(board.totalTaps, 4);

    const http = await (await fetch(`http://${base}/api/scoreboard`)).json();
    assert.equal(http.rounds, 1);
    assert.equal(http.recent[0].taps.A + http.recent[0].taps.B, 4);
    assert.equal((await (await fetch(`http://${base}/api/rounds?limit=5`)).json()).rounds.length, 1);
    clients.forEach(ws => ws.close());
  } finally { await server.closeAll(); }
});

test('without SQLite the game still runs and the scoreboard API says unavailable', async () => {
  const server = createTugServer({ room: {}, scores: null });
  await new Promise(r => server.listen(0, '127.0.0.1', r));
  try {
    const res = await fetch(`http://127.0.0.1:${server.address().port}/api/scoreboard`);
    assert.equal(res.status, 503);
  } finally { await server.closeAll(); }
});
