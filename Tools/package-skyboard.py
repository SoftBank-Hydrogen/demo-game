"""Build the SkyBoard frontend and package a Sky upload.

    python Tools/package-skyboard.py SkyBoard-fullstack.zip                    # main demo (default)
    python Tools/package-skyboard.py SkyBoard-single-server.zip --single-server

full-stack: separate components, as in the source tree
    frontend/  React + Vite source (Sky builds it, or uses static/)
    static/    the built frontend; config.json gets the deployed API address
    backend/   FastAPI app with its Dockerfile and the sample SQLite DB (backend/seed/board.db)
single-server: one FastAPI container that also serves the built frontend from public/
    (same origin; the sample DB sits at data/board.db, the runtime path inside a /app/data volume)
"""
import argparse
import json
import shutil
import subprocess
import zipfile
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
WEB = REPO / "web"
BACKEND, FRONTEND, STATIC = WEB / "backend", WEB / "frontend", WEB / "static"
SKIP_PARTS = {"node_modules", "__pycache__", ".venv", "data", "dist", ".pytest_cache"}


def files(folder: Path, suffixes=None):
    for path in sorted(folder.rglob("*")):
        rel = path.relative_to(folder)
        if path.is_file() and not SKIP_PARTS.intersection(rel.parts) and (suffixes is None or path.suffix in suffixes):
            yield path, rel.as_posix()


def build_frontend():
    npm = shutil.which("npm.cmd") or shutil.which("npm")
    if not npm:
        raise SystemExit("npm is required")
    subprocess.run([npm, "run", "build"], cwd=FRONTEND, check=True)
    if json.loads((STATIC / "config.json").read_text())["apiBaseUrl"] != "":
        raise SystemExit("web/frontend/public/config.json must keep apiBaseUrl empty (set it at deploy time)")


def backend_core(archive: zipfile.ZipFile, prefix: str):
    for name in ("main.py", "requirements.txt", "Dockerfile"):
        archive.write(BACKEND / name, prefix + name)
    for path, rel in files(BACKEND / "board", {".py", ".sql"}):
        archive.write(path, f"{prefix}board/{rel}")


def full_stack(archive: zipfile.ZipFile):
    backend_core(archive, "backend/")
    archive.write(BACKEND / ".dockerignore", "backend/.dockerignore")
    archive.write(BACKEND / "seed/board.db", "backend/seed/board.db")
    for name in ("index.html", "package.json", "package-lock.json", "vite.config.js"):
        archive.write(FRONTEND / name, "frontend/" + name)
    for folder in ("src", "public"):
        for path, rel in files(FRONTEND / folder):
            archive.write(path, f"frontend/{folder}/{rel}")
    for path, rel in files(STATIC):
        archive.write(path, "static/" + rel)
    archive.write(WEB / "README.md", "README.md")


def single_server(archive: zipfile.ZipFile):
    backend_core(archive, "")
    archive.write(BACKEND / "seed/board.db", "data/board.db")   # inside Sky's /app/data volume
    for path, rel in files(STATIC):
        archive.write(path, "public/" + rel)
    archive.writestr(".dockerignore", "__pycache__/\n*.pyc\n.env\n.venv/\n")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("output", type=Path)
    parser.add_argument("--single-server", action="store_true", help="one container serving API and frontend")
    args = parser.parse_args()
    build_frontend()
    args.output.parent.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(args.output, "w", zipfile.ZIP_DEFLATED) as archive:
        (single_server if args.single_server else full_stack)(archive)
    print(args.output.resolve())


if __name__ == "__main__":
    main()
