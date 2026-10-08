'use strict';
const { test } = require('node:test');
const assert = require('node:assert/strict');
const { createRoom } = require('../room');

const fast = { countdownMs: 100, roundMs: 1000, resultMs: 200, random: () => 0.1 };

test('teams are balanced: new players join the smaller team', () => {
  const room = createRoom(fast);
  const teams = ['p1', 'p2', 'p3', 'p4', 'p5'].map(id => room.join(id));
  assert.deepEqual(room.snapshot(0).players, { A: 3, B: 2 });
  assert.equal(teams[0], 'A');     // tie -> random() < .5 -> A
  assert.equal(teams[1], 'B');     // B smaller
  room.leave('p1'); room.leave('p3');
  assert.equal(room.join('p6'), 'A');
});

test('round lifecycle: waiting -> countdown -> playing -> result -> countdown', () => {
  const room = createRoom(fast);
  room.join('a');
  room.tick(0);
  assert.equal(room.snapshot(0).phase, 'waiting');        // needs 2 players
  room.join('b');
  room.tick(0);
  assert.equal(room.snapshot(0).phase, 'countdown');
  assert.equal(room.tap('a', 3, 50), 0);                  // taps before start do not count
  room.tick(100);
  assert.equal(room.snapshot(100).phase, 'playing');
  assert.equal(room.snapshot(100).round, 1);
  assert.equal(room.snapshot(600).remainingMs, 500);
  const result = room.tick(1100);
  assert.equal(result.round, 1);
  assert.equal(room.snapshot(1100).phase, 'result');
  assert.equal(room.tick(1150), null);                    // result reported once
  room.tick(1300);
  assert.equal(room.snapshot(1300).phase, 'countdown');
});

test('winner is the team with more taps; equal is DRAW', () => {
  const room = createRoom(fast);
  room.join('a'); room.join('b');         // a -> A, b -> B
  room.tick(0); room.tick(100);
  room.tap('a', 5, 200);
  room.tap('b', 3, 200);
  const result = room.tick(1100);
  assert.equal(result.winner, 'A');
  assert.deepEqual(result.taps, { A: 5, B: 3 });
  assert.deepEqual(result.players, { A: 1, B: 1 });

  room.tick(1300); room.tick(1400);       // next round
  assert.equal(room.snapshot(1400).taps.A, 0);
  room.tap('a', 2, 1500); room.tap('b', 2, 1500);
  assert.equal(room.tick(2400).winner, 'DRAW');
});

test('each player is limited to 15 counted taps per second', () => {
  const room = createRoom({ ...fast, roundMs: 5000 });
  room.join('a'); room.join('b');
  room.tick(0); room.tick(100);
  assert.equal(room.tap('a', 10, 1000), 10);
  assert.equal(room.tap('a', 10, 1500), 5);    // only 5 left in this second
  assert.equal(room.tap('a', 10, 1900), 0);
  assert.equal(room.tap('a', 10, 2000), 10);   // new second
  assert.equal(room.stats.tapsLimited, 15);
  assert.equal(room.tap('a', 1000, 3000), 15); // one message is capped too
});

test('invalid tap counts and unknown players are ignored', () => {
  const room = createRoom(fast);
  room.join('a'); room.join('b');
  room.tick(0); room.tick(100);
  for (const n of [0, -1, 1.5, '3', null]) assert.equal(room.tap('a', n, 200), 0);
  assert.equal(room.tap('ghost', 1, 200), 0);
  assert.equal(room.snapshot(200).taps.A + room.snapshot(200).taps.B, 0);
});

test('countdown falls back to waiting if a player leaves', () => {
  const room = createRoom(fast);
  room.join('a'); room.join('b');
  room.tick(0);
  room.leave('b');
  room.tick(50);
  assert.equal(room.snapshot(50).phase, 'waiting');
});
