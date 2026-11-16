#!/usr/bin/env python3
# SPDX-License-Identifier: GPL-2.0-or-later
"""Generate independent Google TFLite masks for the existing public portrait fixtures.

Requires selfie-reference-requirements.txt, the original pinned Google model,
and an explicit output directory. No camera or private images are read.
"""
import argparse
import gzip
import hashlib
import json
from pathlib import Path

import mediapipe as mp
from mediapipe.tasks import python
from mediapipe.tasks.python import vision
import numpy as np
from PIL import Image

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("model", type=Path)
parser.add_argument("output", type=Path)
args = parser.parse_args()
assert hashlib.sha256(args.model.read_bytes()).hexdigest() == (
    "191ac9529ae506ee0beefa6b2c945a172dab9d07d1e802a290a4e4038226658b"
), "Unrecognized Google model"
args.output.mkdir(parents=True, exist_ok=True)
fixtures = Path(__file__).resolve().parents[1] / "tests/OpenPhysical.Biometrics.Facial.Tests/Fixtures"
options = vision.ImageSegmenterOptions(
    base_options=python.BaseOptions(model_asset_path=str(args.model)),
    output_category_mask=False,
    output_confidence_masks=True,
)
manifest = {}
with vision.ImageSegmenter.create_from_options(options) as segmenter:
    for name in ("bush", "glenn"):
        rgb = np.asarray(Image.open(fixtures / (name + ".jpg")).convert("RGB").resize(
            (256, 256), Image.Resampling.BILINEAR
        ))
        image = args.output / ("selfie-" + name + "-input.png")
        mask = args.output / ("selfie-" + name + "-mask.f32.gz")
        Image.fromarray(rgb).save(image)
        result = segmenter.segment(mp.Image(image_format=mp.ImageFormat.SRGB, data=rgb))
        assert len(result.confidence_masks) == 1
        values = result.confidence_masks[0].numpy_view().copy().astype("<f4")
        assert values.shape == (256, 256) and np.isfinite(values).all()
        mask.write_bytes(gzip.compress(values.tobytes(), mtime=0))
        for path in (image, mask):
            data = path.read_bytes()
            manifest[path.name] = {"bytes": len(data), "sha256": hashlib.sha256(data).hexdigest()}
print(json.dumps(manifest, indent=2))
