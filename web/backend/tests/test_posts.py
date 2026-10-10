import os
from pathlib import Path

from conftest import PNG, signup, upload


def test_post_crud_with_images(client):
    headers, user = signup(client, "alice")
    img1, img2 = upload(client, headers).json(), upload(client, headers).json()
    assert img1["contentType"] == "image/png" and img1["url"].startswith("/uploads/")
    assert client.get(img1["url"]).content == PNG       # public, so <img> works without a login

    res = client.post("/api/posts", headers=headers, json={"title": " Hello ", "body": "First post", "imageIds": [img1["id"], img2["id"]]})
    assert res.status_code == 201
    post = res.json()
    assert post["title"] == "Hello" and post["author"]["id"] == user["id"]
    assert [i["id"] for i in post["images"]] == [img1["id"], img2["id"]]

    listed = client.get("/api/posts", headers=headers).json()
    assert listed["total"] == 1 and listed["items"][0]["imageCount"] == 2
    assert listed["items"][0]["thumbnail"] == img1["url"]

    # Edit: drop the first image -> its file is deleted.
    res = client.patch(f"/api/posts/{post['id']}", headers=headers, json={"body": "Edited", "imageIds": [img2["id"]]})
    assert res.json()["body"] == "Edited" and res.json()["title"] == "Hello"
    assert client.get(img1["url"]).status_code == 404

    assert client.delete(f"/api/posts/{post['id']}", headers=headers).status_code == 204
    assert client.get(f"/api/posts/{post['id']}", headers=headers).status_code == 404
    assert client.get(img2["url"]).status_code == 404
    assert list(Path(os.environ["UPLOAD_DIR"]).iterdir()) == []


def test_only_author_can_change(client):
    alice, _ = signup(client, "alice")
    bob, _ = signup(client, "bob")
    post = client.post("/api/posts", headers=alice, json={"title": "Mine", "body": "x"}).json()
    assert client.get(f"/api/posts/{post['id']}", headers=bob).status_code == 200
    assert client.patch(f"/api/posts/{post['id']}", headers=bob, json={"title": "Taken"}).status_code == 403
    assert client.delete(f"/api/posts/{post['id']}", headers=bob).status_code == 403


def test_cannot_attach_someone_elses_or_used_image(client):
    alice, _ = signup(client, "alice")
    bob, _ = signup(client, "bob")
    bobs = upload(client, bob).json()
    assert client.post("/api/posts", headers=alice, json={"title": "t", "body": "b", "imageIds": [bobs["id"]]}).status_code == 400
    mine = upload(client, alice).json()
    client.post("/api/posts", headers=alice, json={"title": "t", "body": "b", "imageIds": [mine["id"]]})
    assert client.post("/api/posts", headers=alice, json={"title": "t2", "body": "b", "imageIds": [mine["id"]]}).status_code == 400
    too_many = [upload(client, alice).json()["id"] for _ in range(5)]
    assert client.post("/api/posts", headers=alice, json={"title": "t", "body": "b", "imageIds": too_many}).status_code == 422


def test_upload_rules(client):
    headers, _ = signup(client, "alice")
    svg = b'<svg xmlns="http://www.w3.org/2000/svg"><script>alert(1)</script></svg>'
    assert upload(client, headers, svg, "x.svg").status_code == 415
    assert upload(client, headers, b"not an image", "x.png").status_code == 415   # name does not matter
    assert upload(client, headers, PNG + b"\0" * (1024 * 1024)).status_code == 413
    assert client.post("/api/images", files={"file": ("a.png", PNG, "image/png")}).status_code == 401
    assert client.get("/uploads/../board.db").status_code == 404
    assert client.get("/uploads/notours.png").status_code == 404


def test_list_paging_and_search(client):
    headers, _ = signup(client, "alice")
    for i in range(25):
        client.post("/api/posts", headers=headers, json={"title": f"Post {i}", "body": "100% done_ok" if i == 3 else "text"})
    page2 = client.get("/api/posts?page=2&pageSize=20", headers=headers).json()
    assert page2["total"] == 25 and len(page2["items"]) == 5 and page2["items"][-1]["title"] == "Post 0"
    # % and _ are matched literally, not as wildcards.
    assert [p["title"] for p in client.get("/api/posts?q=100%25", headers=headers).json()["items"]] == ["Post 3"]
    assert client.get("/api/posts?q=e_o", headers=headers).json()["total"] == 1
    assert client.get("/api/posts?q=Pos_", headers=headers).json()["total"] == 0   # "_" would match "Post"
