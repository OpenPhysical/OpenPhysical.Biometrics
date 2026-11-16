#!/usr/bin/env python3
"""Verify the frozen test-only fixture package source tree and its input hashes."""

import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import sys
import xml.etree.ElementTree as ET


ROOT = Path(__file__).resolve().parents[1]
PACKAGE = ROOT / "tests/OpenPhysical.Biometrics.TestFixtures.Package"


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def fail(message: str) -> None:
    raise ValueError(message)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--nbis-root", type=Path)
    parser.add_argument("--nfiq-root", type=Path)
    args = parser.parse_args()
    try:
        manifest_path = PACKAGE / "provenance.json"
        manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
        if manifest.get("schemaVersion") != 1 or manifest.get("testOnly") is not True:
            fail("fixture provenance schema or test-only marker is invalid")
        project = ET.parse(PACKAGE / "OpenPhysical.Biometrics.TestFixtures.Package.csproj").getroot()
        expected_version = project.findtext(".//PackageVersion")
        if not expected_version or manifest.get("packageId") != "OpenPhysical.Biometrics.TestFixtures" or manifest.get("packageVersion") != expected_version:
            fail("fixture package identity or version changed")

        entries = manifest.get("files")
        if not isinstance(entries, list) or not entries:
            fail("fixture provenance does not list package files")
        listed = set()
        for entry in entries:
            relative = Path(entry["path"])
            if relative.is_absolute() or ".." in relative.parts:
                fail(f"unsafe fixture path: {relative}")
            path = PACKAGE / relative
            listed.add(relative.as_posix())
            if not path.is_file() or path.stat().st_size != entry.get("size") or sha256(path) != entry.get("sha256"):
                fail(f"fixture bytes do not match provenance: {relative}")
        actual = {path.relative_to(PACKAGE).as_posix() for path in PACKAGE.rglob("*") if path.is_file() and path != manifest_path
                  and path.name != "packages.lock.json" and not {"bin", "obj"}.intersection(path.relative_to(PACKAGE).parts)}
        if actual != listed:
            fail("fixture package tree has added or missing files relative to provenance.json")

        nbis_manifest = json.loads((PACKAGE / "fixtures/nbis/tests/fixtures/sd04/manifest.json").read_text(encoding="utf-8"))
        images = nbis_manifest.get("images", [])
        if len(images) != 20 or len(nbis_manifest.get("pairs", [])) != 10:
            fail("SD04 manifest must retain all ten pairs and twenty images")
        for image in images:
            path = PACKAGE / "fixtures/nbis/tests/fixtures/sd04" / image["raw"]
            if sha256(path) != image["raw_sha256"]:
                fail(f"SD04 golden bytes changed: {image['raw']}")

        nfiq_vectors = json.loads((PACKAGE / "fixtures/nfiq/tests/fixtures/nfiq2-release-vectors.json").read_text(encoding="utf-8"))
        first = next((item for item in nfiq_vectors.get("vectors", []) if item.get("id") == "SFinGe_Test01"), None)
        nfiq_fixture = PACKAGE / "fixtures/nfiq/tests/fixtures/SFinGe_Test01.gray8"
        if first is None or sha256(nfiq_fixture) != first["raw_sha256"]:
            fail("NFIQ2 SFinGe fixture no longer matches the upstream vector")
        if not (PACKAGE / "fixtures/nbis/tests/fixtures/sd04/NIST-DATA-NOTICE.txt").is_file():
            fail("SD04 NIST data notice is missing")
        if not (PACKAGE / "fixtures/nfiq/tests/fixtures/NIST-DATA-NOTICE.txt").is_file():
            fail("NFIQ NIST data notice is missing")
        if not (PACKAGE / "licenses/NFIQ2-LICENSE.md").is_file() or not (PACKAGE / "licenses/NFIQ2-EXAMPLES-README.md").is_file():
            fail("NFIQ2 release license and example attribution are required")

        optional_sources = {
            "OpenPhysical.Biometrics.NBIS": (args.nbis_root, "d8e67d61af827d179beac51ddc4d933f75d4c695"),
            "OpenPhysical.Biometrics.NFIQ": (args.nfiq_root, "fea2d64e7e93388f460d57fc1219b44d5b48dd1f"),
        }
        for repository, (root, commit) in optional_sources.items():
            if root is None:
                continue
            actual_commit = subprocess.check_output(["git", "-C", str(root), "rev-parse", commit], text=True).strip()
            if actual_commit != commit:
                fail(f"source pin is not available: {repository}@{commit}")
            for item in manifest["sourceInputs"]:
                if item["repository"] != repository:
                    continue
                source_bytes = subprocess.check_output(
                    ["git", "-C", str(root), "show", f"{commit}:{item['sourcePath']}"]
                )
                if hashlib.sha256(source_bytes).hexdigest() != item["sha256"]:
                    fail(f"frozen package input differs from pinned source: {repository}:{item['sourcePath']}")
    except (OSError, KeyError, TypeError, ValueError, json.JSONDecodeError, subprocess.CalledProcessError) as error:
        print(f"Fixture package verification failed: {error}", file=sys.stderr)
        return 1

    print(f"Verified {len(entries)} files in the test-only fixture package; all golden image hashes match.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
