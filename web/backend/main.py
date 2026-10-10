"""SkyBoard API (FastAPI).

Local:   uvicorn main:app --reload --host 127.0.0.1 --port 8000
Deploy:  python -m uvicorn main:app --host 0.0.0.0 --port $PORT
Settings: see board/config.py (DATABASE_PATH, SEED_DATABASE, UPLOAD_DIR or S3_BUCKET, CORS_ORIGINS, ...).
"""
import sqlite3
from contextlib import asynccontextmanager
from pathlib import Path

from fastapi import Depends, FastAPI
from fastapi.middleware.cors import CORSMiddleware
from fastapi.staticfiles import StaticFiles

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

# Also allow a separately hosted frontend when configured.
app.add_middleware(
    CORSMiddleware,
    allow_origins=list(load_settings().cors_origins),
    allow_methods=["GET", "POST", "PATCH", "DELETE"],
    allow_headers=["Authorization", "Content-Type"],
    max_age=600,
)

for module in (auth, images, posts, projects, search):
    app.include_router(module.router)


@app.get("/health")
def health(db: sqlite3.Connection = Depends(get_db)):
    db.execute("SELECT 1").fetchone()
    return {"status": "ok"}


backend_dir = Path(__file__).resolve().parent
static_dir = backend_dir / "public"
if not (static_dir / "index.html").is_file():
    static_dir = backend_dir.parent / "static"

if (static_dir / "index.html").is_file():
    # API and upload routes take precedence over the frontend.
    app.mount("/", StaticFiles(directory=static_dir, html=True), name="frontend")
else:
    @app.get("/")
    def root():
        return {"service": "skyboard-api", "status": "ok", "docs": "/docs"}
