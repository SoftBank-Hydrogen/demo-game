"""Build the frontend and package a single-server Sky upload."""
import argparse
import json
import shutil
import subprocess
import zipfile
from pathlib import Path


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("output", type=Path)
    args = parser.parse_args()
    repo = Path(__file__).resolve().parent.parent
    backend = repo / "web/backend"
    frontend = repo / "web/frontend"
    static = repo / "web/static"
    npm = shutil.which("npm.cmd") or shutil.which("npm")
    if not npm:
        parser.error("npm is required")
    subprocess.run([npm, "run", "build"], cwd=frontend, check=True)
    if json.loads((static / "config.json").read_text())["apiBaseUrl"] != "":
        raise ValueError("The bundled frontend must use the same origin")
    args.output.parent.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(args.output, "w", zipfile.ZIP_DEFLATED) as archive:
        for name in ("main.py", "requirements.txt", "Dockerfile"):
            archive.write(backend / name, name)
        for path in sorted((backend / "board").rglob("*")):
            if path.is_file() and path.suffix in {".py", ".sql"} and "__pycache__" not in path.parts:
                archive.write(path, path.relative_to(backend).as_posix())
        # Seed directly at the runtime path, inside Sky's /app/data volume.
        archive.write(backend / "seed/board.db", "data/board.db")
        for path in sorted(static.rglob("*")):
            if path.is_file():
                archive.write(path, "public/" + path.relative_to(static).as_posix())
        archive.writestr(".dockerignore", "__pycache__/\n*.pyc\n.env\n.venv/\n")
    print(args.output.resolve())


if __name__ == "__main__":
    main()
