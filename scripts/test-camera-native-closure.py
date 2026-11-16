#!/usr/bin/env python3
"""Regression checks for the source-bound dual-architecture camera package closure."""

import importlib.util
import json
from pathlib import Path
import struct
import tempfile
import unittest


SCRIPT = Path(__file__).with_name("camera_native_closure.py")
SPEC = importlib.util.spec_from_file_location("camera_native_closure", SCRIPT)
assert SPEC is not None and SPEC.loader is not None
camera = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(camera)


class CameraNativeClosureTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        self.source = self.root / "native" / "macos"
        self.source.mkdir(parents=True)
        (self.source / "build.sh").write_text("build inputs\n", encoding="utf-8")
        (self.source / "OpenPhysicalCamera.m").write_text("native input\n", encoding="utf-8")
        self.artifacts = self.root / "artifacts" / "camera"
        self.commit = "a" * 40

    def tearDown(self):
        self.temp.cleanup()

    def macho(self, architecture: str) -> bytes:
        cpu = {"x86_64": 0x01000007, "arm64": 0x0100000C}[architecture]
        return struct.pack("<IiiIIIII", 0xFEEDFACF, cpu, 3, 6, 0, 0, 0, 0)

    def create_rid(self, rid: str, architecture: str, binary: bytes | None = None,
                   binary_architecture: str | None = None) -> None:
        rid_root = self.artifacts / rid
        rid_root.mkdir(parents=True, exist_ok=True)
        payload = binary if binary is not None else self.macho(binary_architecture or architecture)
        binary_path = rid_root / "libopenphysical_camera.dylib"
        binary_path.write_bytes(payload)
        record = {
            "schemaVersion": 1,
            "sourceCommit": self.commit,
            "rid": rid,
            "architecture": architecture,
            "sourceFiles": camera.source_inventory(self.source),
            "binary": {"path": binary_path.name, "size": len(payload), "sha256": camera.sha256(payload)},
            "sdkVersion": "test-sdk",
            "compilerVersion": "test-clang",
            "fileType": "Mach-O 64-bit dynamically linked shared library",
            "linkedLibraries": "test system frameworks",
        }
        (rid_root / "camera-native-manifest.json").write_text(json.dumps(record), encoding="utf-8")

    def complete_closure(self) -> None:
        self.create_rid("osx-arm64", "arm64")
        self.create_rid("osx-x64", "x86_64")

    def test_complete_closure_verifies(self):
        self.complete_closure()
        records = camera.verify_closure(self.artifacts, self.commit, self.source)
        self.assertEqual({record["rid"] for record in records}, set(camera.RID_ARCH))

    def test_missing_rid_fails(self):
        self.create_rid("osx-arm64", "arm64")
        with self.assertRaisesRegex(ValueError, "missing osx-x64"):
            camera.verify_closure(self.artifacts, self.commit, self.source)

    def test_wrong_macho_cpu_fails(self):
        self.create_rid("osx-arm64", "arm64", binary_architecture="x86_64")
        self.create_rid("osx-x64", "x86_64")
        with self.assertRaisesRegex(ValueError, "wrong Mach-O architecture"):
            camera.verify_closure(self.artifacts, self.commit, self.source)

    def test_binary_hash_mismatch_fails(self):
        self.complete_closure()
        binary = self.artifacts / "osx-arm64" / "libopenphysical_camera.dylib"
        binary.write_bytes(self.macho("arm64") + b"tampered")
        with self.assertRaisesRegex(ValueError, "hash or size"):
            camera.verify_closure(self.artifacts, self.commit, self.source)

    def test_source_hash_mismatch_fails(self):
        self.complete_closure()
        (self.source / "OpenPhysicalCamera.m").write_text("changed input\n", encoding="utf-8")
        with self.assertRaisesRegex(ValueError, "source hashes differ"):
            camera.verify_closure(self.artifacts, self.commit, self.source)

    def test_wrong_source_commit_fails(self):
        self.complete_closure()
        with self.assertRaisesRegex(ValueError, "stale or different source commit"):
            camera.verify_closure(self.artifacts, "b" * 40, self.source)

    def test_package_missing_architecture_asset_fails(self):
        self.complete_closure()
        records = camera.verify_closure(self.artifacts, self.commit, self.source)
        entries = [{"path": "runtimes/osx-arm64/native/libopenphysical_camera.dylib",
                    "sha256": records[0]["binary"]["sha256"]}]
        with self.assertRaisesRegex(ValueError, "exactly the osx-arm64 and osx-x64"):
            camera.validate_package_native_entries(entries, records)

    def test_package_binary_hash_mismatch_fails(self):
        self.complete_closure()
        records = camera.verify_closure(self.artifacts, self.commit, self.source)
        entries = [{"path": f"runtimes/{record['rid']}/native/libopenphysical_camera.dylib",
                    "sha256": record["binary"]["sha256"]} for record in records]
        entries[1]["sha256"] = "0" * 64
        with self.assertRaisesRegex(ValueError, "differs from the verified native build"):
            camera.validate_package_native_entries(entries, records)


if __name__ == "__main__":
    unittest.main()
