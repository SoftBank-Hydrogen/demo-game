"""TeamBoard API (FastAPI).

Local:   uvicorn main:app --reload --host 127.0.0.1 --port 8000
Deploy:  python -m uvicorn main:app --host 0.0.0.0 --port $PORT
Settings: see board/config.py (DATABASE_PATH, UPLOAD_DIR, CORS_ORIGINS, MAX_UPLOAD_MB, SESSION_DAYS).
"""
import sqlite3
from contextlib import asynccontextmanager

from fastapi import Depends, FastAPI
from fastapi.middleware.cors import CORSMiddleware

from board.config import load_settings
from board.db import get_db, migrate
from board.routes import auth, images, posts, projects, search


@asynccontextmanager
async def lifespan(app: FastAPI):
    app.state.settings = load_settings()
    app.state.settings.upload_dir.mkdir(parents=True, exist_ok=True)
    applied = migrate(app.state.settings.database_path)
    if applied:
        print("Applied migrations:", ", ".join(applied))
    yield


app = FastAPI(title="TeamBoard API", version="0.1.0", lifespan=lifespan)

# The web page is served from a different address (the frontend), so the browser asks first (CORS).
app.add_middleware(
    CORSMiddleware,
    allow_origins=list(load_settings().cors_origins),
    allow_methods=["GET", "POST", "PATCH", "DELETE"],
    allow_headers=["Authorization", "Content-Type"],
    max_age=600,
)

for module in (auth, images, posts, projects, search):
    app.include_router(module.router)


@app.get("/")
def root():
    return {"service": "teamboard-api", "status": "ok", "docs": "/docs"}


@app.get("/health")
def health(db: sqlite3.Connection = Depends(get_db)):
    db.execute("SELECT 1").fetchone()
    return {"status": "ok"}
