# SPDX-License-Identifier: GPL-2.0-or-later
"""Pin the official UNSW 2D samples without assessing quality or matching."""
import argparse
import hashlib
import io
import json
from pathlib import Path
import re
import struct
import subprocess
import tempfile

from PIL import Image
from evaluation_image_decode import unchanged_gray8


ARCHIVE_SHA = "6bae764887655e79f643d9aca925f79cd85274a011903a0031a3dd2ecb3b6560"
SPLIT_SHA = "df2fa2cb52ce6e1de3458ec7ad9813de0c29d70dc0ee6be1d129d745601c2e48"
NAME = re.compile(r"([1-9][0-9]*)_([1-9][0-9]*)_([1-9][0-9]*)[.]bmp", re.IGNORECASE)


def sha(data):
    return hashlib.sha256(data).hexdigest()


def describe(path):
    data = path.read_bytes()
    with Image.open(io.BytesIO(data)) as image:
        if image.format != "BMP" or image.size != (640, 480) or not unchanged_gray8(image):
            raise ValueError("Unexpected geometry, container or grayscale samples: " + path.name)
        if image.info.get("compression") != 0 or struct.unpack_from("<ii", data, 38) != (19685, 19685):
            raise ValueError("Unexpected BMP compression or nominal 500 ppi header: " + path.name)
        pixels = image.tobytes()
    centered = bytearray(b"\xff" * (800 * 750))
    for y in range(480):
        offset = (y + 135) * 800 + 80
        centered[offset:offset + 640] = pixels[y * 640:(y + 1) * 640]
    person, finger, capture = map(int, NAME.fullmatch(path.name).groups())
    return {
        "id": f"unsw_{person}_{finger}_{capture}",
        "personId": str(person), "fingerId": str(finger), "captureId": str(capture),
        "sourceMember": "2D Database/" + path.name,
        "width": 640, "height": 480,
        "sourcePixelsPerMeter": [19685, 19685],
        "sourceSha256": sha(data), "sha256": sha(pixels),
        "centeredSha256": sha(centered),
    }


def inventory(archive, split_path, output):
    if output.exists():
        raise ValueError("Refusing to overwrite an inventory.")
    with archive.open("rb") as stream:
        if hashlib.file_digest(stream, "sha256").hexdigest() != ARCHIVE_SHA:
            raise ValueError("Archive differs from the verified author deposit.")
    split_bytes = split_path.read_bytes()
    if sha(split_bytes) != SPLIT_SHA:
        raise ValueError("Pre-inspection person split changed.")
    split = json.loads(split_bytes)
    with tempfile.TemporaryDirectory(prefix="unsw-2d-integrity-") as temporary:
        subprocess.run(["7z", "x", str(archive.resolve()), "-o" + temporary, "-bd", "-y"],
                       check=True, capture_output=True)
        source = Path(temporary) / "2D Database"
        images = describe_source(source)
    write_manifests(images, split, output)


def describe_source(source):
    files = list(source.iterdir())
    if any(not path.is_file() or path.is_symlink() or not NAME.fullmatch(path.name) for path in files):
        raise ValueError("Unexpected extracted member.")
    slots = [tuple(map(int, NAME.fullmatch(path.name).groups())) for path in files]
    expected = {(p, f, c) for p in range(1, 151) for f in range(1, 11) for c in range(1, 5)}
    if len(slots) != len(set(slots)) or set(slots) != expected:
        raise ValueError("The documented 150-person, ten-finger, four-capture census differs.")
    return [describe(path) for _, path in sorted(zip(slots, files))]


def write_manifests(images, split, output):
    groups = {}
    for image in images:
        groups.setdefault(image["sha256"], []).append(image["id"])
    duplicates = [ids for ids in groups.values() if len(ids) > 1]
    common = {
        "cohort": "UNSW_2D", "archiveSha256": ARCHIVE_SHA,
        "preinspectionSplitSha256": SPLIT_SHA,
        "source": "https://zenodo.org/records/10432121",
        "resolutionPpi": 500,
        "geometry": "640x480 samples centered unchanged on white 800x750; no resampling",
        "identityScope": "Documented person/finger/capture IDs; no inferred anatomical labels",
        "inspection": "Container/header/sample integrity only; no contact, quality, features or matching",
    }
    manifests = {"manifest.json": dict(common, images=images)}
    for role, people in split["Partitions"].items():
        selected = [image for image in images if int(image["personId"]) in people]
        manifests[role + ".manifest.json"] = dict(common, role=role, personIds=people, images=selected)
    manifests["integrity.json"] = dict(common, images=len(images), exactDuplicateGroups=duplicates)
    output.mkdir(parents=True)
    for name, document in manifests.items():
        (output / name).write_text(json.dumps(document, indent=2) + "\n")
    print(json.dumps({"images": len(images), "exactDuplicateGroups": len(duplicates),
                      "manifests": {name: sha((output / name).read_bytes()) for name in manifests}}, indent=2))


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("archive", type=Path)
    parser.add_argument("split", type=Path)
    parser.add_argument("output", type=Path)
    args = parser.parse_args()
    inventory(args.archive, args.split, args.output)
