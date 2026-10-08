'use strict';
// Scoreboard storage: one row per finished round in a local SQLite file.
const fs = require('node:fs');
const path = require('node:path');

const DB_FILE = path.join(__dirname, 'data', 'scores.db');
const MIGRATIONS = path.join(__dirname, 'migrations');

let sqlite = null;
try { sqlite = require('node:sqlite'); } catch { /* Node < 22: scoreboard disabled */ }

function openScores(file = DB_FILE) {
  if (!sqlite) {
    console.warn('Scoreboard disabled: node:sqlite needs Node.js 22 or newer.');
    return null;
  }
  if (file !== ':memory:') fs.mkdirSync(path.dirname(file), { recursive: true });
  const db = new sqlite.DatabaseSync(file);
  for (const name of fs.readdirSync(MIGRATIONS).filter(f => f.endsWith('.sql')).sort()) {
    db.exec(fs.readFileSync(path.join(MIGRATIONS, name), 'utf8'));
  }

  const insert = db.prepare(
    'INSERT INTO rounds (started_at, ended_at, winner, taps_a, taps_b, players_a, players_b) VALUES (?, ?, ?, ?, ?, ?, ?)');
  const recent = db.prepare(
    'SELECT id, started_at, ended_at, winner, taps_a, taps_b, players_a, players_b FROM rounds ORDER BY id DESC LIMIT ?');
  const totals = db.prepare(
    `SELECT COUNT(*) AS rounds,
            SUM(CASE WHEN winner = 'A' THEN 1 ELSE 0 END) AS wins_a,
            SUM(CASE WHEN winner = 'B' THEN 1 ELSE 0 END) AS wins_b,
            SUM(CASE WHEN winner = 'DRAW' THEN 1 ELSE 0 END) AS draws,
            SUM(taps_a + taps_b) AS taps,
            MAX(taps_a + taps_b) AS best_round_taps
       FROM rounds`);

  return {
    saveRound(r) {
      insert.run(r.startedAt, r.endedAt, r.winner, r.taps.A, r.taps.B, r.players.A, r.players.B);
    },
    recentRounds(limit = 10) {
      return recent.all(Math.max(1, Math.min(100, limit))).map(row => ({
        id: row.id, startedAt: row.started_at, endedAt: row.ended_at, winner: row.winner,
        taps: { A: row.taps_a, B: row.taps_b }, players: { A: row.players_a, B: row.players_b },
      }));
    },
    summary() {
      const t = totals.get();
      return {
        rounds: t.rounds, wins: { A: t.wins_a || 0, B: t.wins_b || 0 }, draws: t.draws || 0,
        totalTaps: t.taps || 0, bestRoundTaps: t.best_round_taps || 0,
      };
    },
    close() { db.close(); },
  };
}

module.exports = { openScores, available: () => sqlite !== null };
