"""Sign up, log in, log out, who am I, and the member list."""
import sqlite3

from fastapi import APIRouter, Depends, HTTPException, Request

from ..db import get_db, now
from ..schemas import Login, Register
from ..security import bearer_token, current_user, end_session, hash_password, new_session, verify_password

router = APIRouter(tags=["auth"])


def user_json(row: sqlite3.Row) -> dict:
    return {"id": row["id"], "username": row["username"], "displayName": row["display_name"]}


@router.post("/api/auth/register", status_code=201)
def register(body: Register, request: Request, db: sqlite3.Connection = Depends(get_db)):
    try:
        with db:
            cur = db.execute(
                "INSERT INTO users (username, display_name, password_hash, created_at) VALUES (?, ?, ?, ?)",
                (body.username, body.display_name, hash_password(body.password), now()))
    except sqlite3.IntegrityError:
        raise HTTPException(409, "Username is already taken") from None
    user = db.execute("SELECT * FROM users WHERE id = ?", (cur.lastrowid,)).fetchone()
    token = new_session(db, user["id"], request.app.state.settings.session_days)
    return {"token": token, "user": user_json(user)}


@router.post("/api/auth/login")
def login(body: Login, request: Request, db: sqlite3.Connection = Depends(get_db)):
    user = db.execute("SELECT * FROM users WHERE username = ?", (body.username,)).fetchone()
    # Same message whether the name or the password is wrong.
    if not user or not verify_password(body.password, user["password_hash"]):
        raise HTTPException(401, "Wrong username or password")
    token = new_session(db, user["id"], request.app.state.settings.session_days)
    return {"token": token, "user": user_json(user)}


@router.post("/api/auth/logout", status_code=204)
def logout(request: Request, user=Depends(current_user), db: sqlite3.Connection = Depends(get_db)):
    end_session(db, bearer_token(request))


@router.get("/api/auth/me")
def me(user=Depends(current_user)):
    return user_json(user)


@router.get("/api/users")
def users(user=Depends(current_user), db: sqlite3.Connection = Depends(get_db)):
    """Everyone on the board, for choosing a task's assignee."""
    rows = db.execute("SELECT * FROM users ORDER BY display_name COLLATE NOCASE").fetchall()
    return [user_json(r) for r in rows]
