from __future__ import annotations

import argparse
import hashlib
import shutil
import zipfile
from pathlib import Path

RUNTIME_SHA256 = {
    "MultiCharacterCampaignTOR.dll": "5d43fcdca48bf7bcd45d8f7d421b5bfe53d1dc1165ef163c516b81cf8693d8d7",
    "MultiCharacterCampaignTOR.NativeCreation.dll": "4bda554e179b5d6ff9c529f6e2ca41f42209ed8d30bf33464c82dff3cdde8767",
    "MultiCharacterCampaignTOR.NativeCreation.Legacy.dll": "18022c497e4e671a2075b3df0046396bf5e14acee0152aec3dcfdf7d6f149517",
    "MultiCharacterCampaignTOR.RuntimeCompatibility.v140.dll": "f58b5a3433db0906f35a1f5c014705b6f6c64682d1d533c75cd1f543bdce6522",
    "MultiCharacterCampaignTOR.SettlementPresence.v141.dll": "eed1abbc07414142e2181fe5356d13e71158931fadffafabe6c061c9ad3d3e27",
}


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def copy_tree(source: Path, destination: Path) -> None:
    if destination.exists():
        shutil.rmtree(destination)
    shutil.copytree(source, destination)


def extract_release_notes(changelog: Path, destination: Path, version: str) -> None:
    lines = changelog.read_text(encoding="utf-8").splitlines()
    start = next((i for i, line in enumerate(lines) if line.strip() == f"## {version}"), None)
    if start is None:
        raise SystemExit(f"CHANGELOG.md has no ## {version} section")
    end = next((i for i in range(start + 1, len(lines)) if lines[i].startswith("## ")), len(lines))
    destination.write_text("\n".join(lines[start:end]).strip() + "\n", encoding="utf-8")


def verify_runtime(runtime_dir: Path) -> None:
    for name, expected_hash in RUNTIME_SHA256.items():
        path = runtime_dir / name
        if not path.is_file():
            raise SystemExit(f"Required runtime DLL not found: {path}")
        actual_hash = sha256(path)
        if actual_hash != expected_hash:
            raise SystemExit(f"Runtime DLL hash mismatch for {name}: expected {expected_hash}, got {actual_hash}")


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--runtime-dir", required=True, type=Path)
    parser.add_argument("--source", required=True, type=Path)
    parser.add_argument("--module-metadata", required=True, type=Path)
    parser.add_argument("--identity-dll", required=True, type=Path)
    parser.add_argument("--output-dir", required=True, type=Path)
    parser.add_argument("--version", required=True)
    args = parser.parse_args()

    verify_runtime(args.runtime_dir)
    if not args.identity_dll.is_file():
        raise SystemExit(f"Built identity DLL not found: {args.identity_dll}")

    if args.output_dir.exists():
        shutil.rmtree(args.output_dir)
    args.output_dir.mkdir(parents=True)

    work = args.output_dir / "work"
    module = work / "Modules" / "MultiCharacterCampaignTOR"
    bin_dir = module / "bin" / "Win64_Shipping_Client"
    bin_dir.mkdir(parents=True)

    for name in RUNTIME_SHA256:
        shutil.copy2(args.runtime_dir / name, bin_dir / name)
    shutil.copy2(args.identity_dll, bin_dir / "MultiCharacterCampaignTOR.IdentityGuard.v140.dll")

    copy_tree(args.source, module / "Source" / "CSharp")
    for name in ("SubModule.xml", "README.md", "CHANGELOG.md", "INSTALLATION.txt", "SOURCE_INFO.md"):
        source = args.module_metadata / name
        if not source.is_file():
            raise SystemExit(f"Required module metadata not found: {source}")
        shutil.copy2(source, module / name)

    expected = (*RUNTIME_SHA256.keys(), "MultiCharacterCampaignTOR.IdentityGuard.v140.dll")
    missing = [name for name in expected if not (bin_dir / name).is_file()]
    if missing:
        raise SystemExit("Missing packaged DLLs: " + ", ".join(missing))

    archive_path = args.output_dir / f"MultiCharacterCampaignTOR-v{args.version}-Bannerlord-1.3.15-TOR-1.16.zip"
    with zipfile.ZipFile(archive_path, "w", zipfile.ZIP_DEFLATED, compresslevel=9) as archive:
        for path in sorted((work / "Modules").rglob("*")):
            if path.is_file():
                archive.write(path, path.relative_to(work))

    checksum = args.output_dir / f"{archive_path.name}.sha256"
    checksum.write_text(f"{sha256(archive_path)}  {archive_path.name}\n", encoding="utf-8")
    extract_release_notes(args.module_metadata / "CHANGELOG.md", args.output_dir / "release-notes.md", args.version)
    print(archive_path)
    print(checksum)


if __name__ == "__main__":
    main()
