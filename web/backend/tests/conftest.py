import sys
from pathlib import Path

import pytest
from fastapi.testclient import TestClient

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

PNG = (b"\x89PNG\r\n\x1a\n\x00\x00\x00\rIHDR\x00\x00\x00\x01\x00\x00\x00\x01\x08\x06\x00\x00\x00\x1f\x15\xc4\x89"
       b"\x00\x00\x00\rIDATx\x9cc\xf8\xcf\xc0\xf0\x1f\x00\x05\x00\x01\xff\x89\x99=\x1d\x00\x00\x00\x00IEND\xaeB`\x82")


@pytest.fixture
def client(tmp_path, monkeypatch):
    """A fresh app with its own database and upload folder."""
    monkeypatch.setenv("DATABASE_PATH", str(tmp_path / "board.db"))
    monkeypatch.setenv("UPLOAD_DIR", str(tmp_path / "uploads"))
    monkeypatch.setenv("SEED_DATABASE", "")   # start empty, without the sample data
    monkeypatch.setenv("MAX_UPLOAD_MB", "1")
    from main import app
    with TestClient(app) as c:
        yield c


def signup(client, username="alice"):
    res = client.post("/api/auth/login", json={"username": username, "password": "pw"})
    assert res.status_code == 200 and res.json()["created"], res.text
    return {"Authorization": "Bearer " + res.json()["token"]}, res.json()["user"]


def upload(client, headers, data=PNG, name="a.png"):
    return client.post("/api/images", headers=headers, files={"file": (name, data, "image/png")})
