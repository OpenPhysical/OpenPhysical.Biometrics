#!/usr/bin/env python3
"""Write the immutable source and hash manifest for the test fixture package."""

import argparse
import hashlib
import json
from pathlib import Path
import sys
import xml.etree.ElementTree as ET


ROOT = Path(__file__).resolve().parents[1]
PACKAGE = ROOT / "tests/OpenPhysical.Biometrics.TestFixtures.Package"
NBIS_COMMIT = "d8e67d61af827d179beac51ddc4d933f75d4c695"
NFIQ_COMMIT = "fea2d64e7e93388f460d57fc1219b44d5b48dd1f"
NFIQ2_COMMIT = "76b8c4e0b0541f3deab832b1a496e524edc0b5b6"


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--replace-existing", action="store_true")
    args = parser.parse_args()
    output = PACKAGE / "provenance.json"
    if output.exists() and not args.replace_existing:
        parser.error("provenance.json already exists; use --replace-existing only for an intentional fixture update")
    files = sorted(path for path in PACKAGE.rglob("*") if path.is_file() and path != output
                   and path.name != "packages.lock.json" and not {"bin", "obj"}.intersection(path.relative_to(PACKAGE).parts))
    project = ET.parse(PACKAGE / "OpenPhysical.Biometrics.TestFixtures.Package.csproj").getroot()
    version = project.findtext(".//PackageVersion")
    if not version:
        raise ValueError("Fixture package must declare an immutable version")
    hashes = {path.relative_to(PACKAGE).as_posix(): sha256(path) for path in files}
    entries = [{"path": path.relative_to(PACKAGE).as_posix(), "sha256": hashes[path.relative_to(PACKAGE).as_posix()], "size": path.stat().st_size} for path in files]
    source_inputs = []
    for relative, digest in hashes.items():
        if relative.startswith("fixtures/nbis/"):
            source_inputs.append({"repository": "OpenPhysical.Biometrics.NBIS", "commit": NBIS_COMMIT,
                                  "sourcePath": relative.removeprefix("fixtures/nbis/"), "packagePath": relative, "sha256": digest})
        elif relative.startswith("fixtures/nfiq/"):
            source_inputs.append({"repository": "OpenPhysical.Biometrics.NFIQ", "commit": NFIQ_COMMIT,
                                  "sourcePath": relative.removeprefix("fixtures/nfiq/"), "packagePath": relative, "sha256": digest})
        elif relative == "licenses/NFIQ2-LICENSE.md":
            source_inputs.append({"repository": "NIST NFIQ2", "commit": NFIQ2_COMMIT,
                                  "sourcePath": "LICENSE.md", "packagePath": relative, "sha256": digest})
        elif relative == "licenses/NFIQ2-EXAMPLES-README.md":
            source_inputs.append({"repository": "NIST NFIQ2", "commit": NFIQ2_COMMIT,
                                  "sourcePath": "examples/README.md", "packagePath": relative, "sha256": digest})
    manifest = {
        "schemaVersion": 1,
        "packageId": "OpenPhysical.Biometrics.TestFixtures",
        "packageVersion": version,
        "testOnly": True,
        "sourceRepositories": [
            {"name": "OpenPhysical.Biometrics.NBIS", "url": "https://github.com/OpenPhysical/OpenPhysical.Biometrics.NBIS", "commit": NBIS_COMMIT},
            {"name": "OpenPhysical.Biometrics.NFIQ", "url": "https://github.com/OpenPhysical/OpenPhysical.Biometrics.NFIQ", "commit": NFIQ_COMMIT},
            {"name": "NIST NFIQ2", "url": "https://github.com/usnistgov/NFIQ2", "commit": NFIQ2_COMMIT},
        ],
        "sourceInputs": source_inputs,
        "files": entries,
    }
    output.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    print(f"Wrote {len(entries)} file hashes to {output.relative_to(ROOT)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
