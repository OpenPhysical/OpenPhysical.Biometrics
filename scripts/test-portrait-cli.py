#!/usr/bin/env python3
# SPDX-License-Identifier: GPL-2.0-or-later
"""Exercise installed CLI rejection, report privacy and output protection.

The attributed Bush fixture has an open mouth. Successful encoding, record
round trips and atomic installation are covered by the shared .NET tests;
this smoke test does not claim those positive installed-CLI paths.
"""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import tempfile

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("command", nargs="+", help="Installed biotool, or dotnet followed by its DLL")
args = parser.parse_args()
fixtures = Path(__file__).resolve().parents[1] / "tests/OpenPhysical.Biometrics.Facial.Tests/Fixtures"
source_bytes = (fixtures / "bush.jpg").read_bytes()
source_hash = hashlib.sha256(source_bytes).hexdigest()

with tempfile.TemporaryDirectory(prefix="portrait-cli-", dir=Path(tempfile.gettempdir()).resolve()) as directory:
    work = Path(directory)
    # Exercise input/output collision protection on a disposable public copy.
    source = work / "source.jpg"
    source.write_bytes(source_bytes)
    portrait = work / "portrait.jp2"
    preview = work / "portrait.png"
    record = work / "record.bin"

    def files():
        return {str(path.relative_to(work)): path.read_bytes() for path in work.rglob("*") if path.is_file()}

    def run(*options, expected=2, error_code="", output=portrait):
        before = files()
        process = subprocess.run(
            [*args.command, "face", "prepare", str(source), "--output", str(output), "--json", *map(str, options)],
            text=True,
            capture_output=True,
        )
        assert process.returncode == expected, (process.returncode, process.stderr, process.stdout)
        assert files() == before, "Rejected requests must preserve inputs and every existing output without staging files"
        assert source_hash not in process.stdout + process.stderr, "Default output must not disclose the source image hash"
        report = json.loads(process.stdout)
        if expected == 1:
            assert report == {"Status": "Failed", "Code": error_code}, report
            return
        assert set(report) == {"Status", "FaceCount", "Width", "Height", "Findings"}, "Default JSON must contain structural summaries only"
        assert all(isinstance(code, str) for code in report["Findings"]), report
        assert report["Status"] == "ReviewRequired" and report["FaceCount"] == 1, report
        assert "MouthOpen" in report["Findings"], report
        assert report["Width"] == 480 and report["Height"] == 640, report
        return report

    # Strict rejection creates neither a JPEG 2000 portrait nor its PNG companion.
    run("--preview", preview)
    assert not portrait.exists() and not preview.exists()

    # Existing destinations are refused before processing, including a preview.
    portrait.write_bytes(b"existing public portrait-output sentinel")
    preview.write_bytes(b"existing public preview-output sentinel")
    record.write_bytes(b"existing public record-output sentinel")
    run(expected=1, error_code="PortraitFileAccessFailed")
    run("--preview", preview, expected=1, error_code="PortraitFileAccessFailed", output=work / "new.jp2")
    run("--overwrite", "--preview", preview)

    # An invalid second destination is never installed for a rejected capture.
    # Atomic rollback after successful preparation is a separate shared test.
    blocked = work / "blocked"
    blocked.write_bytes(b"public output-path fixture")
    run("--overwrite", "--preview", blocked / "preview.png")
    run("--overwrite", expected=1, error_code="InvalidPortraitInput", output=source)

    geometry = work / "review.json"
    review = {
        "Version": 1,
        "SourceSha256": source_hash,
        "Width": 808,
        "Height": 1024,
        "FaceIndex": 0,
        "Crown": {"X": 416, "Y": 160},
        "ViewerLeftEar": {"X": 270, "Y": 423},
        "ViewerRightEar": {"X": 615, "Y": 396},
    }
    geometry.write_text(json.dumps(review))
    acquisition = work / "capture.json"
    acquisition.write_text(json.dumps({
        "CapturedAt": "2026-01-01T00:00:00Z",
        "DeviceType": 0,
        "SourceType": 2,
        "FullFrontalConfirmed": True,
        "OpticalResolutionConfirmed": True,
        "Quality": 80,
    }))
    # Reviewed boundaries and claimed acquisition confirmation cannot bypass
    # the automatic strict mouth check or overwrite any requested object.
    reviewed = run("--overwrite", "--geometry", geometry, "--record", record, "--acquisition", acquisition)
    assert "EstimatedHeadBoundary" not in reviewed["Findings"], reviewed
    review["SourceSha256"] = "00" * 32
    geometry.write_text(json.dumps(review))
    run("--overwrite", "--geometry", geometry, expected=1, error_code="InvalidPortraitInput")
    run("--overwrite", "--record", record, expected=1, error_code="InvalidPortraitInput")

print("Installed portrait CLI rejection workflows passed.")
