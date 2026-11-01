#!/usr/bin/env python3
# SPDX-License-Identifier: GPL-2.0-or-later
"""Verify the retained optional facial model assets before builds and packaging."""
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
MODELS = ROOT / "src/OpenPhysical.Biometrics.Onnx/Resources"
for item in json.loads((MODELS / "provenance.json").read_text())["files"]:
    path = MODELS / item["file"]
    if path.stat().st_size != item["bytes"] or hashlib.sha256(path.read_bytes()).hexdigest() != item["sha256"]:
        raise SystemExit("Facial model integrity check failed; retrieve the committed Git LFS assets.")
fixtures = ROOT / "tests/OpenPhysical.Biometrics.Facial.Tests/Fixtures"
for name, item in json.loads((fixtures / "manifest.json").read_text())["files"].items():
    data = (fixtures / name).read_bytes()
    if len(data) != item["bytes"] or hashlib.sha256(data).hexdigest() != item["sha256"]:
        raise SystemExit(f"Portrait fixture integrity check failed: {name}")
print("Verified MediaPipe model assets and portrait regression fixtures.")
