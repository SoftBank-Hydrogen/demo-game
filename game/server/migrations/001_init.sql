CREATE TABLE IF NOT EXISTS rounds (
  id         INTEGER PRIMARY KEY,
  started_at INTEGER NOT NULL,
  ended_at   INTEGER NOT NULL,
  winner     TEXT    NOT NULL,
  taps_a     INTEGER NOT NULL,
  taps_b     INTEGER NOT NULL,
  players_a  INTEGER NOT NULL,
  players_b  INTEGER NOT NULL
);

CREATE INDEX IF NOT EXISTS rounds_ended_at ON rounds (ended_at);
