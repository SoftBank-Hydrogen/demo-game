from conftest import signup


def test_health_and_root(client):
    assert client.get("/").json()["status"] == "ok"
    assert client.get("/health").json() == {"status": "ok"}


def test_register_login_me_logout(client):
    headers, user = signup(client, "alice", "Alice")
    assert client.get("/api/auth/me", headers=headers).json() == user

    res = client.post("/api/auth/login", json={"username": "ALICE", "password": "correct-horse"})
    assert res.status_code == 200          # usernames are case-insensitive
    second = {"Authorization": "Bearer " + res.json()["token"]}

    assert client.post("/api/auth/logout", headers=headers).status_code == 204
    assert client.get("/api/auth/me", headers=headers).status_code == 401
    assert client.get("/api/auth/me", headers=second).status_code == 200   # other login still valid


def test_register_rules(client):
    signup(client, "alice")
    assert client.post("/api/auth/register", json={
        "username": "Alice", "password": "another-pass", "displayName": "A"}).status_code == 409
    for bad in ({"username": "a", "password": "longenough", "displayName": "x"},
                {"username": "bob", "password": "short", "displayName": "x"},
                {"username": "bob smith", "password": "longenough", "displayName": "x"}):
        assert client.post("/api/auth/register", json=bad).status_code == 422


def test_wrong_password_and_missing_token(client):
    signup(client, "alice")
    assert client.post("/api/auth/login", json={"username": "alice", "password": "nope-nope"}).status_code == 401
    assert client.post("/api/auth/login", json={"username": "nobody", "password": "nope-nope"}).status_code == 401
    assert client.get("/api/posts").status_code == 401
    assert client.get("/api/posts", headers={"Authorization": "Bearer made-up"}).status_code == 401


def test_password_is_not_stored_in_plain_text(client):
    import sqlite3
    import os
    signup(client, "alice")
    db = sqlite3.connect(os.environ["DATABASE_PATH"])
    stored = db.execute("SELECT password_hash FROM users").fetchone()[0]
    token_rows = db.execute("SELECT token_hash FROM sessions").fetchall()
    db.close()
    assert stored.startswith("scrypt$") and "correct-horse" not in stored
    assert all(len(row[0]) == 64 for row in token_rows)
