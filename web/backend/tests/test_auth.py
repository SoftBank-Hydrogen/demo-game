from conftest import signup


def test_health_and_root(client):
    assert client.get("/").json()["status"] == "ok"
    assert client.get("/health").json() == {"status": "ok"}


def test_new_name_creates_account_then_login_me_logout(client):
    headers, user = signup(client, "alice")
    assert user["displayName"] == "alice"
    assert client.get("/api/auth/me", headers=headers).json() == user

    res = client.post("/api/auth/login", json={"username": "ALICE", "password": "pw"})
    assert res.status_code == 200 and res.json()["created"] is False   # names are case-insensitive
    second = {"Authorization": "Bearer " + res.json()["token"]}

    assert client.post("/api/auth/logout", headers=headers).status_code == 204
    assert client.get("/api/auth/me", headers=headers).status_code == 401
    assert client.get("/api/auth/me", headers=second).status_code == 200   # other login still valid


def test_any_name_and_short_password_are_fine(client):
    res = client.post("/api/auth/login", json={"username": "김 테스트", "password": "1"})
    assert res.status_code == 200 and res.json()["created"] is True
    for bad in ({"username": "", "password": "x"}, {"username": "   ", "password": "x"},
                {"username": "bob", "password": ""}, {"username": "x" * 31, "password": "x"}):
        assert client.post("/api/auth/login", json=bad).status_code == 422


def test_wrong_password_and_missing_token(client):
    signup(client, "alice")
    assert client.post("/api/auth/login", json={"username": "alice", "password": "nope"}).status_code == 401
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
    assert stored.startswith("scrypt$") and len(stored.split("$")) == 6   # hash, never the password itself
    assert all(len(row[0]) == 64 for row in token_rows)
