"""Where uploaded image files live. All file access goes through here.

- Local folder (default): UPLOAD_DIR, e.g. data/uploads, or a mounted volume on-premises.
- Object storage: set S3_BUCKET (optionally S3_PREFIX, S3_ENDPOINT_URL for S3-compatible storage
  such as MinIO). Credentials come from the standard AWS chain (ECS task role, AWS_* variables, ...),
  never from this code. The bucket can stay private: the API reads images and serves them itself,
  so image URLs (/uploads/<name>) are the same in both modes.
"""
import re
import secrets
from collections.abc import Iterator
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


def new_name(ext: str) -> str:
    return f"{secrets.token_hex(16)}.{ext}"


def url(name: str) -> str:
    """Path the browser loads the image from, relative to the API address."""
    return f"/uploads/{name}"


def content_type(name: str) -> str:
    return CONTENT_TYPES[name.rsplit(".", 1)[1]]


class LocalStorage:
    kind = "local"

    def __init__(self, folder: Path):
        self.folder = folder
        folder.mkdir(parents=True, exist_ok=True)

    def save(self, name: str, data: bytes) -> None:
        (self.folder / name).write_bytes(data)

    def read(self, name: str) -> Iterator[bytes] | None:
        """The file's bytes in chunks, or None if there is no such image."""
        if not FILENAME.fullmatch(name) or not (self.folder / name).is_file():
            return None
        return _chunks((self.folder / name).open("rb"))

    def delete(self, name: str) -> None:
        if FILENAME.fullmatch(name):
            (self.folder / name).unlink(missing_ok=True)

    def check(self) -> None:
        probe = self.folder / ".write-check"
        probe.write_bytes(b"ok")
        probe.unlink()


class S3Storage:
    kind = "s3"

    def __init__(self, bucket: str, prefix: str = "uploads/", endpoint_url: str | None = None):
        import boto3   # only needed in this mode
        from botocore.exceptions import ClientError
        self.bucket, self.prefix, self.ClientError = bucket, prefix, ClientError
        self.client = boto3.client("s3", endpoint_url=endpoint_url or None)

    def save(self, name: str, data: bytes) -> None:
        self.client.put_object(Bucket=self.bucket, Key=self.prefix + name, Body=data, ContentType=content_type(name))

    def read(self, name: str) -> Iterator[bytes] | None:
        if not FILENAME.fullmatch(name):
            return None
        try:
            body = self.client.get_object(Bucket=self.bucket, Key=self.prefix + name)["Body"]
        except self.ClientError as error:
            if error.response.get("Error", {}).get("Code") in {"NoSuchKey", "404"}:
                return None
            raise
        return body.iter_chunks(64 * 1024)

    def delete(self, name: str) -> None:
        if FILENAME.fullmatch(name):
            self.client.delete_object(Bucket=self.bucket, Key=self.prefix + name)

    def check(self) -> None:
        self.client.head_bucket(Bucket=self.bucket)


def make_storage(settings) -> LocalStorage | S3Storage:
    if settings.s3_bucket:
        return S3Storage(settings.s3_bucket, settings.s3_prefix, settings.s3_endpoint_url)
    return LocalStorage(settings.upload_dir)


def _chunks(file, size: int = 64 * 1024) -> Iterator[bytes]:
    with file:
        while chunk := file.read(size):
            yield chunk
