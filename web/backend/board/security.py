"""Passwords (scrypt) and login tokens (random, stored as a SHA-256 hash)."""
import base64
import hashlib
import hmac
import secrets
import sqlite3
from datetime import datetime, timedelta, timezone

from fastapi import Depends, HTTPException, Request

from .db import get_db, now

SCRYPT_N, SCRYPT_R, SCRYPT_P = 2**14, 8, 1


def hash_password(password: str) -> str:
    salt = secrets.token_bytes(16)
    digest = hashlib.scrypt(password.encode(), salt=salt, n=SCRYPT_N, r=SCRYPT_R, p=SCRYPT_P)
    return "scrypt${}${}${}${}${}".format(SCRYPT_N, SCRYPT_R, SCRYPT_P, _b64(salt), _b64(digest))


def verify_password(password: str, stored: str) -> bool:
    try:
        _, n, r, p, salt, digest = stored.split("$")
        actual = hashlib.scrypt(password.encode(), salt=_unb64(salt), n=int(n), r=int(r), p=int(p))
        return hmac.compare_digest(actual, _unb64(digest))
    except ValueError:   # malformed stored value
        return False


def new_session(db: sqlite3.Connection, user_id: int, days: int) -> str:
    """Create a login and return the token the browser keeps (sent back as `Authorization: Bearer ...`)."""
    token = secrets.token_urlsafe(32)
    expires = (datetime.now(timezone.utc) + timedelta(days=days)).strftime("%Y-%m-%dT%H:%M:%SZ")
    with db:
        db.execute("DELETE FROM sessions WHERE expires_at < ?", (now(),))
        db.execute("INSERT INTO sessions VALUES (?, ?, ?, ?)", (_token_hash(token), user_id, now(), expires))
    return token


def end_session(db: sqlite3.Connection, token: str) -> None:
    with db:
        db.execute("DELETE FROM sessions WHERE token_hash = ?", (_token_hash(token),))


def bearer_token(request: Request) -> str | None:
    scheme, _, token = request.headers.get("Authorization", "").partition(" ")
    if scheme.lower() != "bearer" or not token.strip():
        return None
    return token.strip()


def current_user(request: Request, db: sqlite3.Connection = Depends(get_db)) -> sqlite3.Row:
    """FastAPI dependency for routes that need a logged-in user. Responds 401 otherwise."""
    token = bearer_token(request)
    row = token and db.execute(
        "SELECT u.* FROM sessions s JOIN users u ON u.id = s.user_id WHERE s.token_hash = ? AND s.expires_at >= ?",
        (_token_hash(token), now())).fetchone()
    if not row:
        raise HTTPException(401, "Login required")
    return row


def _token_hash(token: str) -> str:
    return hashlib.sha256(token.encode()).hexdigest()


def _b64(data: bytes) -> str:
    return base64.b64encode(data).decode()


def _unb64(text: str) -> bytes:
    return base64.b64decode(text)
