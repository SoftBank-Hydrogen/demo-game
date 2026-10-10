"""Projects (owner may rename or delete) and their tasks (any member may add, move or delete)."""
import sqlite3

from fastapi import APIRouter, Depends, HTTPException

from ..db import get_db, now
from ..schemas import ProjectIn, ProjectPatch, TaskIn, TaskPatch
from ..security import current_user

router = APIRouter(tags=["projects"])

TASK_SQL = """
SELECT t.*, a.username AS assignee_username, a.display_name AS assignee_name
  FROM tasks t LEFT JOIN users a ON a.id = t.assignee_id
"""


def project_json(row: sqlite3.Row) -> dict:
    return {"id": row["id"], "name": row["name"], "description": row["description"],
            "owner": {"id": row["owner_id"], "username": row["username"], "displayName": row["display_name"]},
            "createdAt": row["created_at"], "updatedAt": row["updated_at"]}


def task_json(row: sqlite3.Row) -> dict:
    assignee = None
    if row["assignee_id"] is not None:
        assignee = {"id": row["assignee_id"], "username": row["assignee_username"], "displayName": row["assignee_name"]}
    return {"id": row["id"], "projectId": row["project_id"], "title": row["title"], "description": row["description"],
            "status": row["status"], "assignee": assignee, "dueDate": row["due_date"],
            "createdAt": row["created_at"], "updatedAt": row["updated_at"]}


@router.get("/api/projects")
def list_projects(user=Depends(current_user), db: sqlite3.Connection = Depends(get_db)):
    rows = db.execute("""
        SELECT p.*, u.username, u.display_name,
               SUM(t.status = 'todo') AS todo, SUM(t.status = 'doing') AS doing, SUM(t.status = 'done') AS done
          FROM projects p JOIN users u ON u.id = p.owner_id LEFT JOIN tasks t ON t.project_id = p.id
         GROUP BY p.id ORDER BY p.updated_at DESC""").fetchall()
    return [{**project_json(r), "taskCounts": {s: r[s] or 0 for s in ("todo", "doing", "done")}} for r in rows]


@router.post("/api/projects", status_code=201)
def create_project(body: ProjectIn, user=Depends(current_user), db: sqlite3.Connection = Depends(get_db)):
    with db:
        cur = db.execute("INSERT INTO projects (owner_id, name, description, created_at, updated_at) "
                         "VALUES (?, ?, ?, ?, ?)", (user["id"], body.name, body.description, now(), now()))
    return _full(db, cur.lastrowid)


@router.get("/api/projects/{project_id}")
def get_project(project_id: int, user=Depends(current_user), db: sqlite3.Connection = Depends(get_db)):
    return _full(db, project_id)


@router.patch("/api/projects/{project_id}")
def update_project(project_id: int, body: ProjectPatch, user=Depends(current_user),
                   db: sqlite3.Connection = Depends(get_db)):
    project = _own(db, project_id, user)
    description = project["description"] if body.description is None else body.description
    with db:
        db.execute("UPDATE projects SET name = ?, description = ?, updated_at = ? WHERE id = ?",
                   (body.name or project["name"], description, now(), project_id))
    return _full(db, project_id)


@router.delete("/api/projects/{project_id}", status_code=204)
def delete_project(project_id: int, user=Depends(current_user), db: sqlite3.Connection = Depends(get_db)):
    _own(db, project_id, user)
    with db:
        db.execute("DELETE FROM projects WHERE id = ?", (project_id,))   # tasks go with it


@router.post("/api/projects/{project_id}/tasks", status_code=201)
def create_task(project_id: int, body: TaskIn, user=Depends(current_user), db: sqlite3.Connection = Depends(get_db)):
    _project(db, project_id)
    _check_assignee(db, body.assignee_id)
    with db:
        cur = db.execute(
            "INSERT INTO tasks (project_id, title, description, status, assignee_id, due_date, created_by, "
            "created_at, updated_at) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)",
            (project_id, body.title, body.description, body.status, body.assignee_id,
             body.due_date.isoformat() if body.due_date else None, user["id"], now(), now()))
        _touch(db, project_id)
    return _task(db, cur.lastrowid)


@router.patch("/api/tasks/{task_id}")
def update_task(task_id: int, body: TaskPatch, user=Depends(current_user), db: sqlite3.Connection = Depends(get_db)):
    task = _task_row(db, task_id)
    changes = body.model_dump(exclude_unset=True)   # only the fields the request contained
    if "assignee_id" in changes:
        _check_assignee(db, changes["assignee_id"])
    for field in ("title", "status"):
        if field in changes and changes[field] is None:
            raise HTTPException(422, f"{field} cannot be empty")
    if changes.get("due_date"):
        changes["due_date"] = changes["due_date"].isoformat()
    if "description" in changes and changes["description"] is None:
        changes["description"] = ""
    if changes:
        # Column names come from the TaskPatch fields above, never from user text.
        sets = ", ".join(f"{column} = ?" for column in changes)
        with db:
            db.execute(f"UPDATE tasks SET {sets}, updated_at = ? WHERE id = ?", (*changes.values(), now(), task_id))
            _touch(db, task["project_id"])
    return _task(db, task_id)


@router.delete("/api/tasks/{task_id}", status_code=204)
def delete_task(task_id: int, user=Depends(current_user), db: sqlite3.Connection = Depends(get_db)):
    task = _task_row(db, task_id)
    with db:
        db.execute("DELETE FROM tasks WHERE id = ?", (task_id,))
        _touch(db, task["project_id"])


def _project(db: sqlite3.Connection, project_id: int) -> sqlite3.Row:
    row = db.execute("SELECT p.*, u.username, u.display_name FROM projects p JOIN users u ON u.id = p.owner_id "
                     "WHERE p.id = ?", (project_id,)).fetchone()
    if not row:
        raise HTTPException(404, "Project not found")
    return row


def _own(db: sqlite3.Connection, project_id: int, user: sqlite3.Row) -> sqlite3.Row:
    project = _project(db, project_id)
    if project["owner_id"] != user["id"]:
        raise HTTPException(403, "Only the project owner can change this project")
    return project


def _full(db: sqlite3.Connection, project_id: int) -> dict:
    project = _project(db, project_id)
    tasks = db.execute(TASK_SQL + " WHERE t.project_id = ? ORDER BY t.id", (project_id,)).fetchall()
    return {**project_json(project), "tasks": [task_json(t) for t in tasks]}


def _task_row(db: sqlite3.Connection, task_id: int) -> sqlite3.Row:
    row = db.execute(TASK_SQL + " WHERE t.id = ?", (task_id,)).fetchone()
    if not row:
        raise HTTPException(404, "Task not found")
    return row


def _task(db: sqlite3.Connection, task_id: int) -> dict:
    return task_json(_task_row(db, task_id))


def _check_assignee(db: sqlite3.Connection, user_id: int | None) -> None:
    if user_id is not None and not db.execute("SELECT 1 FROM users WHERE id = ?", (user_id,)).fetchone():
        raise HTTPException(400, "Assignee does not exist")


def _touch(db: sqlite3.Connection, project_id: int) -> None:
    db.execute("UPDATE projects SET updated_at = ? WHERE id = ?", (now(), project_id))
