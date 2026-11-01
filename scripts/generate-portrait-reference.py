#!/usr/bin/env python3
# SPDX-License-Identifier: GPL-2.0-or-later
"""Regenerate portrait vectors with an independently checked-out upstream implementation."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import sys

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("upstream", type=Path, help="Checkout of yakhyo/mediapipe-face-mesh-onnx")
args = parser.parse_args()
revision = subprocess.check_output(["git", "-C", str(args.upstream), "rev-parse", "HEAD"], text=True).strip()
assert revision == "3bdf1e0d100a4c709766c051a0c9e9cdab1fed62", "Wrong upstream revision"
assert not subprocess.check_output(["git", "-C", str(args.upstream), "diff", "HEAD", "--", "models"], text=True), "Upstream model code has changes"
sys.path.insert(0, str(args.upstream.resolve()))
import cv2
import numpy as np
from models import BlazeFace, FaceMesh
from models.onnx_model import roi_from_box, warp_roi

root = Path(__file__).resolve().parents[1]
fixtures = root / "tests/OpenPhysical.Biometrics.Facial.Tests/Fixtures"
models = root / "src/OpenPhysical.Biometrics.Onnx/Resources"
for artifact in json.loads((models / "provenance.json").read_text())["files"]:
    assert hashlib.sha256((models / artifact["file"]).read_bytes()).hexdigest() == artifact["sha256"]
image = cv2.imread(str(fixtures / "glenn.jpg"))
boxes, keypoints, confidence = BlazeFace(str(models / "face_detection_short_range.onnx"), providers=["CPUExecutionProvider"]).detect(image)
mesh = FaceMesh(str(models / "face_landmarker_Nx3x256x256.onnx"), providers=["CPUExecutionProvider"])
landmarks, presence = mesh.predict(image, boxes, keypoints=keypoints)
assert len(boxes) == 1
roi = roi_from_box(boxes[0], keypoints[0])
crop, inverse = warp_roi(image, roi, mesh.input_size)
blob = mesh._preprocess(crop)[None]
output = mesh.session.run(None, {mesh.input_name: blob})[0]
blob.astype("<f4").tofile(fixtures / "glenn.input.f32")
output.astype("<f4").tofile(fixtures / "glenn.output0.f32")
(fixtures / "glenn.reference.json").write_text(json.dumps({"landmarks": landmarks[0].tolist(), "keypoints": keypoints[0].tolist(), "presence": float(presence[0]), "confidence": float(confidence[0])}, indent=2) + "\n")
files = {p.name: {"bytes": p.stat().st_size, "sha256": hashlib.sha256(p.read_bytes()).hexdigest()} for p in sorted(fixtures.iterdir()) if p.suffix in (".jpg", ".jpeg", ".f32") or p.name.endswith(".reference.json")}
(fixtures / "manifest.json").write_text(json.dumps({"revision": revision, "files": files}, indent=2) + "\n")
