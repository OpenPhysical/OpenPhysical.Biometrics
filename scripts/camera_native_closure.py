#!/usr/bin/env python3
"""Build and verify source-bound macOS camera bridge artifacts."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
from pathlib import Path
import platform
import re
import subprocess
import sys


ROOT = Path(__file__).resolve().parents[1]
SOURCE_ROOT = ROOT / "src/OpenPhysical.Biometrics.Capture.Camera/native/macos"
RID_ARCH = {"osx-arm64": "arm64", "osx-x64": "x86_64"}
CPU_TYPES = {0x01000007: "x86_64", 0x0100000C: "arm64"}
COMMIT_RE = re.compile(r"^[0-9a-f]{40}$")


def sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def source_inventory(source_root: Path = SOURCE_ROOT) -> list[dict[str, object]]:
    files = sorted(path for path in source_root.iterdir() if path.is_file())
    if not files or not (source_root / "build.sh").is_file():
        raise ValueError(f"camera native sources are missing under {source_root}")
    result = []
    for path in files:
        if path.suffix not in (".h", ".m", ".sh"):
            continue
        relative = path.resolve().relative_to(ROOT.resolve()).as_posix() if ROOT.resolve() in path.resolve().parents else path.name
        payload = path.read_bytes()
        result.append({"path": relative, "size": len(payload), "sha256": sha256(payload)})
    if not any(item["path"].endswith("build.sh") for item in result):
        raise ValueError("camera native build script is absent from the source inventory")
    return result


def mach_o_architectures(data: bytes) -> set[str]:
    if len(data) < 8:
        raise ValueError("camera native artifact is not a Mach-O binary")
    magics = {
        b"\xce\xfa\xed\xfe": ("<", "thin"),
        b"\xfe\xed\xfa\xce": (">", "thin"),
        b"\xcf\xfa\xed\xfe": ("<", "thin"),
        b"\xfe\xed\xfa\xcf": (">", "thin"),
        b"\xca\xfe\xba\xbe": (">", "fat32"),
        b"\xbe\xba\xfe\xca": ("<", "fat32"),
        b"\xca\xfe\xba\xbf": (">", "fat64"),
        b"\xbf\xba\xfe\xca": ("<", "fat64"),
    }
    header = magics.get(data[:4])
    if header is None:
        raise ValueError("camera native artifact has an unknown Mach-O header")
    endian, kind = header

    def architecture(cpu_type: int) -> str:
        value = CPU_TYPES.get(cpu_type & 0xFFFFFFFF)
        if value is None:
            raise ValueError(f"camera native artifact has unsupported Mach-O CPU type 0x{cpu_type:08x}")
        return value

    if kind == "thin":
        return {architecture(int.from_bytes(data[4:8], "little" if endian == "<" else "big"))}
    count = int.from_bytes(data[4:8], "little" if endian == "<" else "big")
    entry_size = 20 if kind == "fat32" else 32
    if count < 1 or count > 16 or len(data) < 8 + count * entry_size:
        raise ValueError("camera native artifact has a malformed universal Mach-O header")
    return {architecture(int.from_bytes(data[8 + index * entry_size:12 + index * entry_size],
                                        "little" if endian == "<" else "big")) for index in range(count)}


def validate_record(record: dict, rid: str, source_commit: str, source_files: list[dict],
                    binary_path: Path) -> dict:
    if rid not in RID_ARCH:
        raise ValueError(f"unsupported camera runtime identifier: {rid}")
    arch = RID_ARCH[rid]
    if record.get("sourceCommit") != source_commit:
        raise ValueError(f"{rid} native build record has a stale or different source commit")
    if record.get("rid") != rid or record.get("architecture") != arch:
        raise ValueError(f"{rid} native build record declares a different RID or architecture")
    if record.get("sourceFiles") != source_files:
        raise ValueError(f"{rid} native build source hashes differ from the checked-out source")
    binary = record.get("binary")
    if not isinstance(binary, dict) or binary.get("path") != binary_path.name:
        raise ValueError(f"{rid} native build record has no matching binary")
    payload = binary_path.read_bytes()
    if binary.get("size") != len(payload) or binary.get("sha256") != sha256(payload):
        raise ValueError(f"{rid} native binary hash or size does not match its source-bound record")
    if mach_o_architectures(payload) != {arch}:
        raise ValueError(f"{rid} native binary has the wrong Mach-O architecture")
    for key in ("sdkVersion", "compilerVersion", "fileType", "linkedLibraries"):
        if not isinstance(record.get(key), str) or not record[key].strip():
            raise ValueError(f"{rid} native build record is missing {key}")
    return record


def verify_closure(native_root: Path, source_commit: str, source_root: Path = SOURCE_ROOT) -> list[dict]:
    if not COMMIT_RE.fullmatch(source_commit):
        raise ValueError("source commit must be a full 40-character Git SHA")
    if not native_root.is_dir():
        raise ValueError("camera native artifact root is missing")
    source_files = source_inventory(source_root)
    records = []
    for rid, arch in RID_ARCH.items():
        rid_root = native_root / rid
        manifest_path = rid_root / "camera-native-manifest.json"
        binary_path = rid_root / "libopenphysical_camera.dylib"
        if not manifest_path.is_file() or not binary_path.is_file():
            raise ValueError(f"camera native closure is missing {rid} manifest or binary")
        record = json.loads(manifest_path.read_text(encoding="utf-8"))
        records.append(validate_record(record, rid, source_commit, source_files, binary_path))
    actual = {child.name for child in native_root.iterdir() if child.is_dir()}
    if actual != set(RID_ARCH) or any(not child.is_dir() for child in native_root.iterdir()):
        raise ValueError(f"camera native closure must contain exactly both macOS RIDs; found {sorted(actual)}")
    return records


def validate_package_native_entries(entries: list[dict], records: list[dict]) -> None:
    expected_paths = {
        f"runtimes/{rid}/native/libopenphysical_camera.dylib": rid for rid in RID_ARCH
    }
    native_entries = [entry for entry in entries
                      if str(entry.get("path", "")).startswith("runtimes/")
                      and "/native/" in str(entry.get("path", ""))]
    native = {entry.get("path"): entry for entry in native_entries}
    if len(native) != len(native_entries):
        raise ValueError("camera NuGet package contains duplicate native asset entries")
    if set(native) != set(expected_paths):
        raise ValueError("camera NuGet package must contain exactly the osx-arm64 and osx-x64 native assets")
    builds = {record.get("rid"): record for record in records}
    if set(builds) != set(RID_ARCH):
        raise ValueError("camera native build evidence is missing an architecture")
    for path, rid in expected_paths.items():
        expected = builds[rid]["binary"]
        if native[path].get("sha256") != expected.get("sha256"):
            raise ValueError(f"camera NuGet asset {path} differs from the verified native build")


def run_checked(command: list[str]) -> str:
    return subprocess.check_output(command, cwd=ROOT, text=True, stderr=subprocess.STDOUT).strip()


def build_one(rid: str, source_commit: str, native_root: Path) -> dict:
    if platform.system() != "Darwin":
        raise ValueError("camera native artifacts must be built on macOS with the Apple SDK")
    if rid not in RID_ARCH:
        raise ValueError(f"unsupported camera runtime identifier: {rid}")
    if not COMMIT_RE.fullmatch(source_commit):
        raise ValueError("source commit must be a full 40-character Git SHA")
    actual = run_checked(["git", "rev-parse", "HEAD"])
    if actual != source_commit:
        raise ValueError(f"checkout {actual} does not match requested source {source_commit}")

    arch = RID_ARCH[rid]
    rid_root = native_root / rid
    rid_root.mkdir(parents=True, exist_ok=True)
    binary_path = rid_root / "libopenphysical_camera.dylib"
    build_script = SOURCE_ROOT / "build.sh"
    environment = dict(os.environ, OPBC_ARCHS=arch)
    subprocess.run(["sh", str(build_script), str(binary_path)], cwd=ROOT, env=environment, check=True)
    sdk_version = run_checked(["xcrun", "--sdk", "macosx", "--show-sdk-version"])
    compiler_version = run_checked(["xcrun", "--sdk", "macosx", "clang", "--version"])
    file_type = run_checked(["file", str(binary_path)])
    linked_libraries = run_checked(["otool", "-L", str(binary_path)])
    binary = binary_path.read_bytes()
    record = {
        "schemaVersion": 1,
        "sourceCommit": source_commit,
        "rid": rid,
        "architecture": arch,
        "sourceFiles": source_inventory(),
        "binary": {"path": binary_path.name, "size": len(binary), "sha256": sha256(binary)},
        "sdkVersion": sdk_version,
        "compilerVersion": compiler_version,
        "fileType": file_type,
        "linkedLibraries": linked_libraries,
    }
    validate_record(record, rid, source_commit, source_inventory(), binary_path)
    (rid_root / "camera-native-manifest.json").write_text(json.dumps(record, indent=2) + "\n", encoding="utf-8")
    return record


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    subparsers = parser.add_subparsers(dest="command", required=True)
    build_parser = subparsers.add_parser("build")
    build_parser.add_argument("--rid", required=True, choices=sorted(RID_ARCH))
    build_parser.add_argument("--source-commit", required=True)
    build_parser.add_argument("--native-root", type=Path, required=True)
    verify_parser = subparsers.add_parser("verify")
    verify_parser.add_argument("--source-commit", required=True)
    verify_parser.add_argument("--native-root", type=Path, required=True)
    args = parser.parse_args()
    try:
        if args.command == "build":
            record = build_one(args.rid, args.source_commit.lower(), args.native_root)
            print(f"Built {record['rid']} camera bridge, SHA-256 {record['binary']['sha256']}.")
        else:
            records = verify_closure(args.native_root, args.source_commit.lower())
            print("Verified camera bridge closure: " + ", ".join(
                f"{item['rid']} {item['binary']['sha256']}" for item in records))
    except (OSError, ValueError, json.JSONDecodeError, subprocess.CalledProcessError) as error:
        print(f"Camera native closure blocked: {error}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
