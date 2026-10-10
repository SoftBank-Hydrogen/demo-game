"""SQLite access: one connection per request, migrations applied once at startup."""
import sqlite3
from collections.abc import Iterator
from datetime import datetime, timezone
from pathlib import Path

from fastapi import Request

MIGRATIONS = Path(__file__).resolve().parent.parent / "migrations"


def connect(path: Path) -> sqlite3.Connection:
    path.parent.mkdir(parents=True, exist_ok=True)
    # FastAPI may run a request's dependency and handler on different worker threads.
    conn = sqlite3.connect(path, check_same_thread=False, timeout=5)
    conn.row_factory = sqlite3.Row
    conn.execute("PRAGMA foreign_keys = ON")
    return conn


def migrate(path: Path) -> list[str]:
    """Apply migrations/*.sql that have not run yet, in name order. Returns the names applied."""
    conn = connect(path)
    try:
        conn.execute("PRAGMA journal_mode = WAL")   # readers do not block the writer
        conn.execute("CREATE TABLE IF NOT EXISTS schema_migrations (name TEXT PRIMARY KEY, applied_at TEXT NOT NULL)")
        done = {row["name"] for row in conn.execute("SELECT name FROM schema_migrations")}
        applied = []
        for file in sorted(MIGRATIONS.glob("*.sql")):
            if file.name in done:
                continue
            with conn:   # one transaction per file
                conn.executescript("BEGIN;" + file.read_text(encoding="utf-8"))
                conn.execute("INSERT INTO schema_migrations VALUES (?, ?)", (file.name, now()))
            applied.append(file.name)
        return applied
    finally:
        conn.close()


def get_db(request: Request) -> Iterator[sqlite3.Connection]:
    """FastAPI dependency: `db: sqlite3.Connection = Depends(get_db)`."""
    conn = connect(request.app.state.settings.database_path)
    try:
        yield conn
    finally:
        conn.close()


def now() -> str:
    return datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


def like(text: str) -> str:
    """Pattern for `LIKE ? ESCAPE '\\'` that matches `text` literally anywhere."""
    return "%" + text.replace("\\", "\\\\").replace("%", "\\%").replace("_", "\\_") + "%"
