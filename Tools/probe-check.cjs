'use strict';
// Use while no players are playing: compare game state before and after one probe.
const assert = require('node:assert/strict');
const { randomBytes } = require('node:crypto');
const { performance } = require('node:perf_hooks');
const WebSocket = require('../game/server/node_modules/ws');
const endpoint = process.argv[2] || 'ws://127.0.0.1:8080/ws';
const statsUrl = new URL('/stats', endpoint.replace(/^ws/, 'http'));

(async () => {
  const before = await (await fetch(statsUrl)).json();
  assert.equal(before.connections.players, 0, 'Run this check without active players');
  const nonce = randomBytes(16).toString('hex');
  const ws = new WebSocket(endpoint);
  const seen = [];
  let timer;
  try {
    const result = await new Promise((resolve, reject) => {
      let sentAt;
      timer = setTimeout(() => reject(new Error('WebSocket connection timeout')), 5000);
      ws.on('error', reject);
      ws.on('open', () => {
        clearTimeout(timer);
        timer = setTimeout(() => reject(new Error('exchange=unverified: no matching nonce within 5s')), 5000);
        sentAt = performance.now();
        ws.send(JSON.stringify({ type: 'sky.probe', nonce }));
      });
      ws.on('message', data => {
        const m = JSON.parse(data); seen.push(m.type);
        if (m.type === 'sky.probe.ack' && m.nonce === nonce) {
          clearTimeout(timer);
          resolve({ exchange: 'verified', nonce, elapsedMs: Math.round((performance.now() - sentAt) * 100) / 100, response: m });
        }
      });
    });
    await new Promise(resolve => setTimeout(resolve, 200));
    const after = await (await fetch(statsUrl)).json();
    assert.deepEqual(seen, ['sky.probe.ack']);
    assert.equal(after.connections.players, before.connections.players);
    assert.deepEqual(after.teams, before.teams);
    assert.equal(after.totals.tapsAccepted, before.totals.tapsAccepted);
    assert.equal(after.phase, before.phase);
    assert.equal(after.round, before.round);
    assert.ok(result.elapsedMs < 5000);
    console.log(JSON.stringify({ ...result, gameUnaffected: true, receivedTypes: seen }, null, 2));
  } finally { clearTimeout(timer); ws.terminate(); }
})().catch(e => { console.error(e.message); process.exitCode = 1; });
