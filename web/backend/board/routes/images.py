"""Upload an image (then attach it to a post by id) and serve stored images."""
import sqlite3

from fastapi import APIRouter, Depends, File, HTTPException, Request, UploadFile
from fastapi.responses import StreamingResponse
from starlette.concurrency import run_in_threadpool

from .. import storage
from ..db import get_db, now
from ..security import current_user

router = APIRouter(tags=["images"])


def image_json(row: sqlite3.Row) -> dict:
    return {"id": row["id"], "url": storage.url(row["filename"]), "contentType": row["content_type"], "size": row["size"]}


@router.post("/api/images", status_code=201)
async def upload(request: Request, file: UploadFile = File(...), user=Depends(current_user),
                 db: sqlite3.Connection = Depends(get_db)):
    settings = request.app.state.settings
    if not settings.image_uploads:
        raise HTTPException(403, "Image uploads are turned off on this server")
    data = await file.read(settings.max_upload_bytes + 1)
    if len(data) > settings.max_upload_bytes:
        raise HTTPException(413, f"Images can be at most {settings.max_upload_bytes // (1024 * 1024)} MB")
    kind = storage.detect(data)
    if not kind:
        raise HTTPException(415, "Only PNG, JPEG, GIF and WebP images are accepted")
    content_type, ext = kind
    name = storage.new_name(ext)
    # Saving may be a network call (S3); keep it off the event loop.
    await run_in_threadpool(request.app.state.storage.save, name, data)
    with db:
        row = db.execute(
            "INSERT INTO images (owner_id, filename, content_type, size, created_at) VALUES (?, ?, ?, ?, ?) RETURNING *",
            (user["id"], name, content_type, len(data), now())).fetchone()
    return image_json(row)


@router.get("/uploads/{name}")
def serve(name: str, request: Request):
    """Public so <img> tags work without a login header; names are random and unguessable."""
    chunks = request.app.state.storage.read(name)
    if chunks is None:
        raise HTTPException(404, "Not found")
    return StreamingResponse(chunks, media_type=storage.content_type(name), headers={
        "Cache-Control": "public, max-age=31536000, immutable",   # a name never gets new content
        "X-Content-Type-Options": "nosniff",
    })
