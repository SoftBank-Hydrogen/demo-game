"""SQLite access: one connection per request; tables created at startup from schema.sql."""
import shutil
import sqlite3
from collections.abc import Iterator
from datetime import datetime, timezone
from pathlib import Path

from fastapi import Request

SCHEMA = Path(__file__).resolve().parent / "schema.sql"


def connect(path: Path) -> sqlite3.Connection:
    path.parent.mkdir(parents=True, exist_ok=True)
    # FastAPI may run a request's dependency and handler on different worker threads.
    conn = sqlite3.connect(path, check_same_thread=False, timeout=5)
    conn.row_factory = sqlite3.Row
    conn.execute("PRAGMA foreign_keys = ON")
    return conn


def init_db(path: Path, seed: Path | None = None) -> bool:
    """Create the database if needed. A missing database starts as a copy of `seed` (sample data)
    when one is given. Returns True if the seed was copied."""
    seeded = False
    if seed and seed.is_file() and not path.exists():
        path.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(seed, path)
        seeded = True
    conn = connect(path)
    try:
        conn.executescript(SCHEMA.read_text(encoding="utf-8"))
    finally:
        conn.close()
    return seeded


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
    """Pattern for `lower(column) LIKE ? ESCAPE '\\'`: matches `text` literally, ignoring case."""
    return "%" + text.lower().replace("\\", "\\\\").replace("%", "\\%").replace("_", "\\_") + "%"
