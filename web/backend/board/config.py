"""Settings come from environment variables so the same code runs locally and when deployed."""
import os
from dataclasses import dataclass
from pathlib import Path

BACKEND_DIR = Path(__file__).resolve().parent.parent


@dataclass(frozen=True)
class Settings:
    database_path: Path          # SQLite file
    seed_database: Path | None   # copied to database_path when that does not exist yet
    upload_dir: Path             # folder for uploaded images
    cors_origins: tuple[str, ...]  # web pages allowed to call this API ("*" = any)
    max_upload_bytes: int
    session_days: int


def load_settings() -> Settings:
    # Any origin by default: logins travel in the Authorization header, not cookies, so this is safe,
    # and the web page's address (e.g. a CloudFront URL) is often unknown when the API is deployed.
    origins = os.environ.get("CORS_ORIGINS", "*")
    seed = os.environ.get("SEED_DATABASE", str(BACKEND_DIR / "seed" / "board.db"))
    return Settings(
        database_path=Path(os.environ.get("DATABASE_PATH", BACKEND_DIR / "data" / "board.db")),
        seed_database=Path(seed) if seed else None,
        upload_dir=Path(os.environ.get("UPLOAD_DIR", BACKEND_DIR / "data" / "uploads")),
        cors_origins=tuple(o.strip().rstrip("/") for o in origins.split(",") if o.strip()),
        max_upload_bytes=int(os.environ.get("MAX_UPLOAD_MB", "5")) * 1024 * 1024,
        session_days=int(os.environ.get("SESSION_DAYS", "7")),
    )
