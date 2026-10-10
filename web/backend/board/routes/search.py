"""One search box over posts, projects and tasks (simple text match, newest first)."""
import sqlite3

from fastapi import APIRouter, Depends, Query

from ..db import get_db, like
from ..security import current_user
from .projects import TASK_SQL, task_json

router = APIRouter(tags=["search"])
LIMIT = 20


@router.get("/api/search")
def search(q: str = Query(..., min_length=1, max_length=100), user=Depends(current_user),
           db: sqlite3.Connection = Depends(get_db)):
    args = (like(q.strip()),) * 2 + (LIMIT,)
    posts = db.execute(
        "SELECT id, title, substr(body, 1, 140) AS excerpt, created_at FROM posts "
        "WHERE lower(title) LIKE ? ESCAPE '\\' OR lower(body) LIKE ? ESCAPE '\\' ORDER BY id DESC LIMIT ?", args)
    projects = db.execute(
        "SELECT id, name, description FROM projects "
        "WHERE lower(name) LIKE ? ESCAPE '\\' OR lower(description) LIKE ? ESCAPE '\\' ORDER BY id DESC LIMIT ?", args)
    tasks = db.execute(
        TASK_SQL + " WHERE lower(t.title) LIKE ? ESCAPE '\\' OR lower(t.description) LIKE ? ESCAPE '\\' "
        "ORDER BY t.id DESC LIMIT ?", args)
    return {
        "query": q,
        "posts": [{"id": r["id"], "title": r["title"], "excerpt": r["excerpt"], "createdAt": r["created_at"]} for r in posts],
        "projects": [dict(r) for r in projects],
        "tasks": [task_json(r) for r in tasks],
    }
