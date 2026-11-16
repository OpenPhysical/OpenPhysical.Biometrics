#!/usr/bin/env python3
"""Generate synthetic numeric oracles with the pinned development-only OpenCV build.

Run with the prototype venv (opencv-python-headless 5.0.0.93, numpy 2.4.6).
No fingerprint image, hardware transfer, vendor SDK, or upstream source is read.
"""
import hashlib
import importlib.metadata
import json
import platform
import re
from pathlib import Path

import cv2
import numpy as np

ROOT = Path(__file__).resolve().parents[2]
FIXTURES = ROOT / "tests/OpenPhysical.Biometrics.Capture.Futronic.Tests/Fixtures"
PROFILE = "fs50-cubic-half128-v1"


def maps(fixture):
    """Independently evaluate the documented factory fixed-point polynomial."""
    def trunc(value, divisor):
        return (abs(value) // divisor) * (-1 if value < 0 else 1)

    scaled = []
    for index, value in enumerate(fixture["coefficients"]):
        first = trunc(value * (32 if index % 6 < 3 else 4096), 25 if index % 6 < 3 else 3125)
        scaled.append(trunc(first * 32, 25))
    controls = bytes.fromhex(fixture["controlsHex"])
    y, x = np.mgrid[0:750, 0:800].astype(np.int64)
    x += controls[3] - 640
    y += controls[4] - 416

    def component(c, center):
        quadratic = (c[3] * x * x + c[4] * x * y + c[5] * y * y) >> 10
        value = ((c[1] * x + c[2] * y + quadratic) >> 7) + center * 128
        return (value / 128).astype(np.float32)

    return component(scaled[:6], 640), component(scaled[6:], 416)


def pixels(kind, seed):
    """Portable uint32 xorshift sequence or a fully specified synthetic pattern."""
    if kind == "xorshift32":
        raw = bytearray(1280 * 832)
        state = seed
        for index in range(len(raw)):
            state ^= (state << 13) & 0xffffffff
            state ^= state >> 17
            state ^= (state << 5) & 0xffffffff
            raw[index] = state >> 24
        return np.frombuffer(raw, np.uint8).reshape(832, 1280)
    y, x = np.mgrid[0:832, 0:1280]
    if kind == "horizontal-ramp":
        return (x // 5).astype(np.uint8)
    if kind == "vertical-ramp":
        return (y % 256).astype(np.uint8)
    if kind == "checkerboard":
        return (((x + y) & 1) * 255).astype(np.uint8)
    raise ValueError("Unknown synthetic pattern")


def main():
    if cv2.__version__ != "5.0.0" or importlib.metadata.version("opencv-python-headless") != "5.0.0.93":
        raise RuntimeError("Use the pinned OpenCV 5.0.0.93 oracle; a different build requires review")
    fixture = json.loads((FIXTURES / "fs50-geometry-v1.json").read_text())
    map_x, map_y = maps(fixture)
    coefficient_source = (ROOT / "src/OpenPhysical.Biometrics.Capture.Futronic/Fs50CubicCoefficients.cs").read_text()
    encoded = "".join(re.findall(r'"([0-9A-F]{64})"', coefficient_source))
    bits = [int(encoded[index:index + 4], 16) for index in range(0, len(encoded), 4)]
    assert len(bits) == 512
    weights = np.asarray(bits, dtype="<u2").tobytes()
    output = {
        "schemaVersion": 1,
        "profileId": PROFILE,
        "oracle": {
            "package": "opencv-python-headless==5.0.0.93",
            "numpy": np.__version__,
            "platform": platform.system() + "-" + platform.machine(),
            "mapType": "float32 exact factory coordinates (multiples of 1/128)",
            "sourceType": "uint8 grayscale",
            "operation": "255 - cv2.remap(source, map_x, map_y, INTER_CUBIC, BORDER_CONSTANT, 0)",
        },
        "coefficientRecovery": "Synthetic 8192-row 1D corpus, NumPy PCG64 seed 51; common cubic coefficients fitted to binary16 FMA observations. No upstream cubic source used.",
        "coefficientBitsLittleEndianSha256": hashlib.sha256(weights).hexdigest(),
        "mapXSha256": hashlib.sha256(map_x.astype("<f4").tobytes()).hexdigest(),
        "mapYSha256": hashlib.sha256(map_y.astype("<f4").tobytes()).hexdigest(),
        "randomGenerator": "uint32 state=seed; for each byte: state ^= state<<13; state ^= state>>17; state ^= state<<5; truncate each left shift to uint32; byte=state>>24",
        "frames": [],
    }
    cases = [("xorshift32", 0x46533530, "factory"), ("xorshift32", 0x12345678, "factory"),
             ("xorshift32", 0xdeadbeef, "factory"), ("horizontal-ramp", 0, "factory"),
             ("vertical-ramp", 0, "factory"), ("checkerboard", 0, "factory"),
             ("xorshift32", 0x13579bdf, "near-origin"), ("xorshift32", 0x2468ace1, "far-border")]
    positions = [0, 1, 18, 19, 127, 799, 800, 12345, 300000, 599200, 599999]
    for kind, seed, geometry_name in cases:
        geometry = fixture
        if geometry_name != "factory":
            offset_x, offset_y, scale_x, scale_y = (1, 1, 10008, 10008) if geometry_name == "near-origin" else (255, 82, 15420, 9995)
            controls = bytearray.fromhex(fixture["controlsHex"])
            controls[3], controls[4] = offset_x, offset_y
            coefficients = [0] * 12
            coefficients[1], coefficients[8] = scale_x, scale_y
            geometry = {"descriptorHex": fixture["descriptorHex"], "controlsHex": controls.hex(), "coefficients": coefficients}
        frame_map_x, frame_map_y = maps(geometry)
        raw = pixels(kind, seed)
        corrected = 255 - cv2.remap(raw, frame_map_x, frame_map_y, cv2.INTER_CUBIC, borderMode=cv2.BORDER_CONSTANT, borderValue=0)
        frame = {
            "kind": kind, "seed": seed, "geometryName": geometry_name,
            "rawSha256": hashlib.sha256(raw.tobytes()).hexdigest(),
            "correctedSha256": hashlib.sha256(corrected.tobytes()).hexdigest(),
            "samples": [{"offset": position, "value": int(corrected.flat[position])} for position in positions],
        }
        if geometry_name != "factory":
            frame["geometry"] = geometry
            frame["mapBounds"] = {"minX": float(frame_map_x.min()), "maxX": float(frame_map_x.max()),
                                  "minY": float(frame_map_y.min()), "maxY": float(frame_map_y.max())}
        output["frames"].append(frame)
    target = FIXTURES / "fs50-resampler-half128-v1.json"
    target.write_text(json.dumps(output, indent=2) + "\n")
    print(f"Wrote {len(cases)} synthetic full-frame hash oracles to {target}")


if __name__ == "__main__":
    main()
