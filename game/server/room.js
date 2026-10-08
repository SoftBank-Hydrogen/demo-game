'use strict';
// One shared room: two teams tap as fast as they can for 30 seconds.
// Pure game logic. No networking here, so it can be tested with a fake clock.

const TEAMS = ['A', 'B'];

const DEFAULTS = {
  countdownMs: 3000,
  roundMs: 30000,
  resultMs: 5000,
  minPlayers: 2,
  tapsPerSecondLimit: 15,   // per player
  maxTapsPerMessage: 20,
};

function createRoom(options = {}) {
  const cfg = { ...DEFAULTS, ...options };
  const random = cfg.random || Math.random;

  const players = new Map();   // id -> { team, windowSecond, windowTaps }
  let phase = 'waiting';       // waiting | countdown | playing | result
  let phaseEndsAt = 0;
  let round = 0;
  let roundStartedAt = 0;
  let taps = { A: 0, B: 0 };
  let lastResult = null;

  const stats = { tapsAccepted: 0, tapsLimited: 0, tapsOutsideRound: 0, perSecond: new Map() };

  function teamSizes() {
    const sizes = { A: 0, B: 0 };
    for (const p of players.values()) sizes[p.team]++;
    return sizes;
  }

  function join(id) {
    const sizes = teamSizes();
    let team;
    if (sizes.A === sizes.B) team = random() < 0.5 ? 'A' : 'B';
    else team = sizes.A < sizes.B ? 'A' : 'B';
    players.set(id, { team, windowSecond: -1, windowTaps: 0 });
    return team;
  }

  function leave(id) {
    players.delete(id);
  }

  // Returns how many of the n taps were counted.
  function tap(id, n, now) {
    const player = players.get(id);
    if (!player) return 0;
    if (!Number.isInteger(n) || n < 1) return 0;
    n = Math.min(n, cfg.maxTapsPerMessage);
    if (phase !== 'playing') { stats.tapsOutsideRound += n; return 0; }

    const second = Math.floor(now / 1000);
    if (player.windowSecond !== second) { player.windowSecond = second; player.windowTaps = 0; }
    const accepted = Math.max(0, Math.min(n, cfg.tapsPerSecondLimit - player.windowTaps));
    player.windowTaps += accepted;
    stats.tapsLimited += n - accepted;
    if (accepted === 0) return 0;

    taps[player.team] += accepted;
    stats.tapsAccepted += accepted;
    stats.perSecond.set(second, (stats.perSecond.get(second) || 0) + accepted);
    return accepted;
  }

  function startCountdown(now) {
    phase = 'countdown';
    phaseEndsAt = now + cfg.countdownMs;
  }

  // Advances the phase. Returns a finished round result once, otherwise null.
  function tick(now) {
    const enoughPlayers = players.size >= cfg.minPlayers;
    if (phase === 'waiting') {
      if (enoughPlayers) startCountdown(now);
      return null;
    }
    if (now < phaseEndsAt) {
      if (phase === 'countdown' && !enoughPlayers) phase = 'waiting';
      return null;
    }
    if (phase === 'countdown') {
      if (!enoughPlayers) { phase = 'waiting'; return null; }
      phase = 'playing';
      round++;
      roundStartedAt = now;
      phaseEndsAt = now + cfg.roundMs;
      taps = { A: 0, B: 0 };
      return null;
    }
    if (phase === 'playing') {
      const sizes = teamSizes();
      lastResult = {
        round,
        winner: taps.A === taps.B ? 'DRAW' : taps.A > taps.B ? 'A' : 'B',
        taps: { ...taps },
        players: sizes,
        startedAt: roundStartedAt,
        endedAt: now,
      };
      phase = 'result';
      phaseEndsAt = now + cfg.resultMs;
      return lastResult;
    }
    // result -> next round or wait
    if (enoughPlayers) startCountdown(now);
    else phase = 'waiting';
    return null;
  }

  function snapshot(now) {
    const remainingMs = phase === 'waiting' ? 0 : Math.max(0, phaseEndsAt - now);
    return { phase, round, remainingMs, taps: { ...taps }, players: teamSizes() };
  }

  function tapsLastSecond(now) {
    const second = Math.floor(now / 1000);
    for (const key of stats.perSecond.keys()) if (key < second - 2) stats.perSecond.delete(key);
    return stats.perSecond.get(second - 1) || 0;
  }

  return {
    join, leave, tap, tick, snapshot, tapsLastSecond,
    teamOf: id => players.get(id)?.team,
    get playerCount() { return players.size; },
    get lastResult() { return lastResult; },
    stats,
  };
}

module.exports = { createRoom, TEAMS, DEFAULTS };
