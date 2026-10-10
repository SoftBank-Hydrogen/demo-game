"""Board posts: list/search, read, write, edit and delete (only by the author), with up to 4 images."""
import sqlite3

from fastapi import APIRouter, Depends, HTTPException, Query, Request

from .. import storage
from ..db import get_db, like, now
from ..schemas import PostIn, PostPatch
from ..security import current_user
from .images import image_json

router = APIRouter(prefix="/api/posts", tags=["posts"])

LIST_SQL = """
SELECT p.*, u.username, u.display_name,
       (SELECT COUNT(*) FROM post_images WHERE post_id = p.id) AS image_count,
       (SELECT i.filename FROM post_images pi JOIN images i ON i.id = pi.image_id
         WHERE pi.post_id = p.id ORDER BY pi.position LIMIT 1) AS thumbnail
  FROM posts p JOIN users u ON u.id = p.author_id
 WHERE ? = '' OR lower(p.title) LIKE ? ESCAPE '\\' OR lower(p.body) LIKE ? ESCAPE '\\'
"""


def post_json(row: sqlite3.Row) -> dict:
    return {
        "id": row["id"], "title": row["title"],
        "author": {"id": row["author_id"], "username": row["username"], "displayName": row["display_name"]},
        "createdAt": row["created_at"], "updatedAt": row["updated_at"],
    }


@router.get("")
def list_posts(q: str = Query("", max_length=100), page: int = Query(1, ge=1),
               page_size: int = Query(20, ge=1, le=50, alias="pageSize"),
               user=Depends(current_user), db: sqlite3.Connection = Depends(get_db)):
    q = q.strip()
    args = (q, like(q), like(q))
    total = db.execute(f"SELECT COUNT(*) FROM ({LIST_SQL}) AS matches", args).fetchone()[0]
    rows = db.execute(LIST_SQL + " ORDER BY p.id DESC LIMIT ? OFFSET ?", args + (page_size, (page - 1) * page_size))
    items = [{**post_json(r), "excerpt": r["body"][:140], "imageCount": r["image_count"],
              "thumbnail": storage.url(r["thumbnail"]) if r["thumbnail"] else None} for r in rows]
    return {"items": items, "total": total, "page": page, "pageSize": page_size}


@router.get("/{post_id}")
def get_post(post_id: int, user=Depends(current_user), db: sqlite3.Connection = Depends(get_db)):
    return _full(db, post_id)


@router.post("", status_code=201)
def create_post(body: PostIn, user=Depends(current_user), db: sqlite3.Connection = Depends(get_db)):
    with db:
        post_id = db.execute("INSERT INTO posts (author_id, title, body, created_at, updated_at) "
                             "VALUES (?, ?, ?, ?, ?) RETURNING id",
                             (user["id"], body.title, body.body, now(), now())).fetchone()[0]
        _set_images(db, post_id, user["id"], body.image_ids)
    return _full(db, post_id)


@router.patch("/{post_id}")
def update_post(post_id: int, body: PostPatch, request: Request, user=Depends(current_user),
                db: sqlite3.Connection = Depends(get_db)):
    post = _own(db, post_id, user)
    removed = []
    with db:
        db.execute("UPDATE posts SET title = ?, body = ?, updated_at = ? WHERE id = ?",
                   (body.title or post["title"], body.body or post["body"], now(), post_id))
        if body.image_ids is not None:
            removed = _set_images(db, post_id, user["id"], body.image_ids)
    _delete_files(request, removed)
    return _full(db, post_id)


@router.delete("/{post_id}", status_code=204)
def delete_post(post_id: int, request: Request, user=Depends(current_user), db: sqlite3.Connection = Depends(get_db)):
    _own(db, post_id, user)
    with db:
        removed = _set_images(db, post_id, user["id"], [])
        db.execute("DELETE FROM posts WHERE id = ?", (post_id,))
    _delete_files(request, removed)


def _full(db: sqlite3.Connection, post_id: int) -> dict:
    row = db.execute("SELECT p.*, u.username, u.display_name FROM posts p JOIN users u ON u.id = p.author_id "
                     "WHERE p.id = ?", (post_id,)).fetchone()
    if not row:
        raise HTTPException(404, "Post not found")
    images = db.execute("SELECT i.* FROM post_images pi JOIN images i ON i.id = pi.image_id "
                        "WHERE pi.post_id = ? ORDER BY pi.position", (post_id,)).fetchall()
    return {**post_json(row), "body": row["body"], "images": [image_json(i) for i in images]}


def _own(db: sqlite3.Connection, post_id: int, user: sqlite3.Row) -> sqlite3.Row:
    post = db.execute("SELECT * FROM posts WHERE id = ?", (post_id,)).fetchone()
    if not post:
        raise HTTPException(404, "Post not found")
    if post["author_id"] != user["id"]:
        raise HTTPException(403, "Only the author can change this post")
    return post


def _set_images(db: sqlite3.Connection, post_id: int, user_id: int, image_ids: list[int]) -> list[str]:
    """Make `image_ids` (in this order) the post's images. Images taken off the post are removed from
    the database; their file names are returned so the files can be deleted after the commit."""
    ids = list(dict.fromkeys(image_ids))   # drop duplicates, keep order
    for image_id in ids:
        image = db.execute("SELECT owner_id FROM images WHERE id = ?", (image_id,)).fetchone()
        used = db.execute("SELECT 1 FROM post_images WHERE image_id = ? AND post_id != ?", (image_id, post_id)).fetchone()
        if not image or image["owner_id"] != user_id or used:
            raise HTTPException(400, f"Image {image_id} cannot be attached")
    current = db.execute("SELECT i.id, i.filename FROM post_images pi JOIN images i ON i.id = pi.image_id "
                         "WHERE pi.post_id = ?", (post_id,)).fetchall()
    removed = [image for image in current if image["id"] not in ids]
    db.execute("DELETE FROM post_images WHERE post_id = ?", (post_id,))
    for position, image_id in enumerate(ids):
        db.execute("INSERT INTO post_images (post_id, image_id, position) VALUES (?, ?, ?)", (post_id, image_id, position))
    for image in removed:
        db.execute("DELETE FROM images WHERE id = ?", (image["id"],))
    return [image["filename"] for image in removed]


def _delete_files(request: Request, names: list[str]) -> None:
    for name in names:
        request.app.state.storage.delete(name)
