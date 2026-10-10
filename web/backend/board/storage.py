"""Image files on local disk (UPLOAD_DIR). All file access goes through here,
so moving images to object storage (e.g. S3) only changes this module."""
import re
import secrets
from pathlib import Path

# Detected from the file's first bytes, never from the name or the browser's claim.
# SVG is not accepted: it can carry scripts.
SIGNATURES = (
    (b"\x89PNG\r\n\x1a\n", "image/png", "png"),
    (b"\xff\xd8\xff", "image/jpeg", "jpg"),
    (b"GIF87a", "image/gif", "gif"),
    (b"GIF89a", "image/gif", "gif"),
)
CONTENT_TYPES = {"png": "image/png", "jpg": "image/jpeg", "gif": "image/gif", "webp": "image/webp"}
FILENAME = re.compile(r"[0-9a-f]{32}\.(png|jpg|gif|webp)")


def detect(data: bytes) -> tuple[str, str] | None:
    """(content type, extension) for a supported image, else None."""
    for magic, content_type, ext in SIGNATURES:
        if data.startswith(magic):
            return content_type, ext
    if data[:4] == b"RIFF" and data[8:12] == b"WEBP":
        return "image/webp", "webp"
    return None


def save(upload_dir: Path, data: bytes, ext: str) -> str:
    """Store the bytes under a new random name and return that name."""
    upload_dir.mkdir(parents=True, exist_ok=True)
    name = f"{secrets.token_hex(16)}.{ext}"
    (upload_dir / name).write_bytes(data)
    return name


def path_of(upload_dir: Path, name: str) -> Path | None:
    """Location of a stored file, or None if the name is not one we could have created."""
    if not FILENAME.fullmatch(name):
        return None
    path = upload_dir / name
    return path if path.is_file() else None


def delete(upload_dir: Path, name: str) -> None:
    path = path_of(upload_dir, name)
    if path:
        path.unlink(missing_ok=True)


def url(name: str) -> str:
    """Path the browser loads the image from, relative to the API address."""
    return f"/uploads/{name}"
