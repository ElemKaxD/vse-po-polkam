"""Package, verify and unpack the complete WebGL player; Python 3 stdlib only."""
import argparse
import hashlib
import json
import re
from pathlib import Path, PurePosixPath
import tempfile
import zipfile

HERE = Path(__file__).resolve().parent
ARCHIVE = HERE / "game.zip"
MANIFEST = HERE / "manifest.json"
ROOT_FILES = {"index.html", "style.css", "run_webgl.py", "StartGame.cmd"}
FOLDERS = {"Build", "Images", "StreamingAssets"}


def digest(data):
    return hashlib.sha256(data).hexdigest()


def validate_paths(files):
    for name in files:
        p = PurePosixPath(name)
        if p.is_absolute() or ".." in p.parts or "\\" in name or ":" in name:
            raise ValueError(f"Unsafe archive path: {name}")
    for name in ("index.html", "run_webgl.py"):
        if name not in files:
            raise ValueError(f"Missing player file: {name}")
    for suffix in (".loader.js", ".data.unityweb", ".wasm.unityweb", ".framework.js.unityweb"):
        if not any(n.startswith("Build/") and n.endswith(suffix) for n in files):
            raise ValueError(f"Missing Unity player artifact: {suffix}")


def validate_index(html, files):
    references = re.findall(r'(?:loaderUrl|dataUrl|frameworkUrl|codeUrl)\s*[:=]\s*buildUrl\s*\+\s*"/([^\"]+)"', html)
    if "{{{" in html or len(references) != 4:
        raise ValueError("index.html is not a completed Unity build")
    for name in references:
        if "Build/" + name not in files:
            raise ValueError(f"index.html references a missing player file: {name}")


def pack(source):
    source = source.resolve()
    files = {}
    for file in sorted(source.rglob("*")):
        if not file.is_file():
            continue
        relative = file.relative_to(source).as_posix()
        if relative not in ROOT_FILES and relative.split("/")[0] not in FOLDERS:
            continue
        data = file.read_bytes()
        files[relative] = {"bytes": len(data), "sha256": digest(data)}
    validate_paths(files)
    validate_index((source / "index.html").read_text(encoding="utf-8"), files)
    # Unity artifacts are already Brotli-compressed. Avoid wasting CPU recompressing.
    with zipfile.ZipFile(ARCHIVE, "w", compression=zipfile.ZIP_STORED) as z:
        for name in files:
            z.write(source / name, name)
    if ARCHIVE.stat().st_size >= 100 * 1024 * 1024:
        raise ValueError("Archive is too large for an ordinary GitHub Git file")
    manifest = {"archive": ARCHIVE.name, "sha256": digest(ARCHIVE.read_bytes()), "files": files}
    MANIFEST.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"Packaged {len(files)} files, {ARCHIVE.stat().st_size / 1048576:.1f} MiB")


def verify():
    manifest = json.loads(MANIFEST.read_text(encoding="utf-8"))
    if digest(ARCHIVE.read_bytes()) != manifest["sha256"]:
        raise ValueError("Archive SHA-256 does not match manifest.json")
    validate_paths(manifest["files"])
    with zipfile.ZipFile(ARCHIVE) as z:
        if len(z.namelist()) != len(set(z.namelist())):
            raise ValueError("Archive has duplicate members")
        if set(z.namelist()) != set(manifest["files"]):
            raise ValueError("Archive file list does not match manifest.json")
        validate_index(z.read("index.html").decode("utf-8"), manifest["files"])
        for name, expected in manifest["files"].items():
            data = z.read(name)
            if len(data) != expected["bytes"] or digest(data) != expected["sha256"]:
                raise ValueError(f"Corrupt player file: {name}")
    return manifest


def unpack(destination=None):
    verify()
    root = destination.resolve() if destination else Path(tempfile.mkdtemp(prefix="aos-webgl-"))
    if root.exists() and any(root.iterdir()):
        raise ValueError(f"Destination is not empty; refusing to overwrite: {root}")
    root.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(ARCHIVE) as z:
        z.extractall(root)
    print(root)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest="command", required=True)
    package = sub.add_parser("pack")
    package.add_argument("--source", type=Path, default=HERE.parent / "Builds" / "WebGL")
    sub.add_parser("check")
    extract = sub.add_parser("unpack")
    extract.add_argument("--output", type=Path)
    args = parser.parse_args()
    if args.command == "pack":
        pack(args.source)
    elif args.command == "check":
        manifest = verify()
        print(f"OK: {len(manifest['files'])} complete, verified player files")
    else:
        unpack(args.output)


if __name__ == "__main__":
    main()
