"""Images in an S3 bucket (S3_BUCKET set). Uses moto's in-memory S3, so no AWS account is needed."""
import os
from pathlib import Path

import boto3
import pytest
from fastapi.testclient import TestClient
from moto import mock_aws

from conftest import PNG, signup, upload


@pytest.fixture
def s3(tmp_path, monkeypatch):
    for name, value in {"AWS_ACCESS_KEY_ID": "test", "AWS_SECRET_ACCESS_KEY": "test", "AWS_DEFAULT_REGION": "us-east-1",
                        "DATABASE_PATH": str(tmp_path / "board.db"), "UPLOAD_DIR": str(tmp_path / "uploads"),
                        "SEED_DATABASE": "", "S3_BUCKET": "skyboard-test", "S3_PREFIX": "images/"}.items():
        monkeypatch.setenv(name, value)
    with mock_aws():
        bucket = boto3.client("s3")
        bucket.create_bucket(Bucket="skyboard-test")
        yield bucket


def keys(bucket):
    return sorted(o["Key"] for o in bucket.list_objects_v2(Bucket="skyboard-test").get("Contents", []))


def test_images_go_to_the_bucket(s3):
    from main import app
    with TestClient(app) as client:
        headers, _ = signup(client, "alice")
        image = upload(client, headers).json()
        name = image["url"].rsplit("/", 1)[1]
        assert keys(s3) == [f"images/{name}"]
        stored = s3.get_object(Bucket="skyboard-test", Key=f"images/{name}")
        assert stored["ContentType"] == "image/png" and stored["Body"].read() == PNG

        res = client.get(image["url"])          # same URL as local mode; the API reads the bucket
        assert res.status_code == 200 and res.content == PNG and res.headers["content-type"] == "image/png"
        assert client.get("/uploads/" + "0" * 32 + ".png").status_code == 404

        post = client.post("/api/posts", headers=headers, json={"title": "t", "body": "b", "imageIds": [image["id"]]}).json()
        client.delete(f"/api/posts/{post['id']}", headers=headers)
        assert keys(s3) == []
        assert not Path(os.environ["UPLOAD_DIR"]).exists() or not any(Path(os.environ["UPLOAD_DIR"]).iterdir())


def test_missing_bucket_stops_startup(s3, monkeypatch):
    monkeypatch.setenv("S3_BUCKET", "no-such-bucket")
    from main import app
    with pytest.raises(RuntimeError, match="no-such-bucket"):
        with TestClient(app):
            pass
