#!/usr/bin/env python3
# SPDX-License-Identifier: GPL-2.0-or-later
"""Exercise the installed portrait CLI with committed, attributed fixtures."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import shutil
import tempfile

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("command", nargs="+", help="Installed biotool, or dotnet followed by its DLL")
args = parser.parse_args()
fixtures = Path(__file__).resolve().parents[1] / "tests/OpenPhysical.Biometrics.Facial.Tests/Fixtures"
source = fixtures / "bush.jpg"
with tempfile.TemporaryDirectory(prefix="portrait-cli-") as directory:
    work = Path(directory)
    portrait = work / "portrait.jp2"
    preview = work / "portrait.png"
    def run(*options, expected=0):
        process = subprocess.run([*args.command, "face", "prepare", str(source), "--output", str(portrait), "--json", *map(str, options)], text=True, capture_output=True)
        assert process.returncode == expected, (process.returncode, process.stderr, process.stdout)
        report = json.loads(process.stdout)
        assert "Landmarks" not in process.stdout and "SourceSha256" not in process.stdout, "Default output must contain structural summaries only"
        return report
    report = run("--preview", preview)
    assert report["Status"] == "Prepared" and report["FaceCount"] == 1
    original = portrait.read_bytes()
    decoder = shutil.which("opj_decompress")
    if decoder:
        decoded = work / "independent.ppm"
        checked = subprocess.run([decoder, "-i", str(portrait), "-o", str(decoded)], capture_output=True)
        assert checked.returncode == 0, checked.stderr
        # OpenJPEG reads the encoded codestream independently of CoreJ2K.
        header = decoded.read_bytes().split(b"\n", 4)
        assert b"480 640" in decoded.read_bytes()[:128]
    assert len(original) * 15 >= 480 * 640 * 3
    assert preview.read_bytes()[:8] == b"\x89PNG\r\n\x1a\n"
    run(expected=1)
    assert portrait.read_bytes() == original
    # Failure while staging a second output must leave the existing portrait intact.
    run("--overwrite", "--preview", work / "missing" / "preview.png", expected=1)
    assert portrait.read_bytes() == original
    assert not list(work.glob(".portrait-*"))
    geometry = work / "review.json"
    review = {"Version": 1, "SourceSha256": hashlib.sha256(source.read_bytes()).hexdigest(), "Width": 808, "Height": 1024, "FaceIndex": 0, "Crown": {"X": 416, "Y": 160}, "ViewerLeftEar": {"X": 270, "Y": 423}, "ViewerRightEar": {"X": 615, "Y": 396}}
    geometry.write_text(json.dumps(review))
    acquisition = work / "capture.json"
    acquisition.write_text(json.dumps({"CapturedAt": "2026-01-01T00:00:00Z", "DeviceType": 0, "SourceType": 2, "FullFrontalConfirmed": True, "OpticalResolutionConfirmed": True, "Quality": 80}))
    record = work / "record.bin"
    run("--overwrite", "--geometry", geometry, "--record", record, "--acquisition", acquisition)
    assert record.read_bytes()[:4] == b"FAC\x00"
    inspected = subprocess.run([*args.command, "inspect", str(record), "--json"], capture_output=True, text=True)
    assert inspected.returncode == 0, inspected.stderr
    json.loads(inspected.stdout)
    reviewed = portrait.read_bytes()
    review["SourceSha256"] = "00" * 32
    geometry.write_text(json.dumps(review))
    run("--overwrite", "--geometry", geometry, expected=1)
    assert portrait.read_bytes() == reviewed
    # Issuance requires explicit reviewed boundaries as well as acquisition evidence.
    run("--overwrite", "--record", record, "--acquisition", acquisition, expected=2)
    assert portrait.read_bytes() == reviewed
print("Installed portrait CLI workflows passed.")
