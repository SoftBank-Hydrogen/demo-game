"""SkyBoard API (FastAPI).

Local:   uvicorn main:app --reload --host 127.0.0.1 --port 8000
Deploy:  python -m uvicorn main:app --host 0.0.0.0 --port $PORT
Settings: see board/config.py (DATABASE_PATH, SEED_DATABASE, UPLOAD_DIR or S3_BUCKET, CORS_ORIGINS, ...).
"""
import sqlite3
from contextlib import asynccontextmanager

from fastapi import Depends, FastAPI
from fastapi.middleware.cors import CORSMiddleware

from board.config import load_settings
from board.db import get_db, init_db
from board.routes import auth, images, posts, projects, search
from board.storage import make_storage


@asynccontextmanager
async def lifespan(app: FastAPI):
    settings = app.state.settings = load_settings()
    storage = app.state.storage = make_storage(settings)
    try:
        storage.check()   # fail at startup, not on the first upload
    except Exception as error:
        where = f"S3 bucket {settings.s3_bucket!r}" if settings.s3_bucket else f"folder {settings.upload_dir}"
        raise RuntimeError(f"Image storage is not usable ({where}): {error}") from error
    print("Images are stored in", f"S3 bucket {settings.s3_bucket}" if settings.s3_bucket else settings.upload_dir)
    if init_db(settings.database_path, settings.seed_database):
        print("New database started from the sample data in", settings.seed_database)
    yield


app = FastAPI(title="SkyBoard API", version="0.2.0", lifespan=lifespan)

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
    return {"service": "skyboard-api", "status": "ok", "docs": "/docs"}


@app.get("/health")
def health(db: sqlite3.Connection = Depends(get_db)):
    db.execute("SELECT 1").fetchone()
    return {"status": "ok"}
