"""Rebuild seed/board.db: sample members, posts and a project, so a fresh deployment is not empty.

    .venv/Scripts/python.exe seed.py

A new database (DATABASE_PATH missing) starts as a copy of this file. Sample logins: mina / demo, jun / demo.
"""
import sqlite3
from pathlib import Path

from board.db import SCHEMA
from board.security import hash_password

SEED = Path(__file__).resolve().parent / "seed" / "board.db"
T = "2026-10-10T09:00:00Z"


def build(path: Path = SEED) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.unlink(missing_ok=True)
    db = sqlite3.connect(path)
    db.executescript(SCHEMA.read_text(encoding="utf-8"))
    password = hash_password("demo")
    db.executemany("INSERT INTO users (id, username, display_name, password_hash, created_at) VALUES (?, ?, ?, ?, ?)",
                   [(1, "mina", "Mina", password, T), (2, "jun", "Jun", password, T)])
    db.executemany("INSERT INTO posts (id, author_id, title, body, created_at, updated_at) VALUES (?, ?, ?, ?, ?, ?)", [
        (1, 1, "Welcome to SkyBoard", "Post updates, attach images and track project tasks here.\n"
                                      "Log in with any new name to join.", T, T),
        (2, 2, "Demo day checklist", "Booth opens at 10.\nBring the QR poster and a charger.", T, T),
    ])
    db.execute("INSERT INTO projects (id, owner_id, name, description, created_at, updated_at) VALUES (?, ?, ?, ?, ?, ?)",
               (1, 1, "Demo day", "Everything for the hackathon booth", T, T))
    db.executemany("INSERT INTO tasks (project_id, title, description, status, assignee_id, due_date, created_by, "
                   "created_at, updated_at) VALUES (1, ?, '', ?, ?, ?, 1, ?, ?)", [
                       ("Print QR poster", "done", 2, "2026-10-20", T, T),
                       ("Rehearse the pitch", "doing", 1, "2026-10-21", T, T),
                       ("Prepare backup laptop", "todo", None, None, T, T),
                   ])
    db.commit()
    db.close()


if __name__ == "__main__":
    build()
    print("Wrote", SEED)
