# SPDX-License-Identifier: GPL-2.0-or-later
"""Strict lossless decoding shared by pinned fingerprint evaluation entry points."""
import hashlib
import io
import json
from pathlib import Path
import sys
import zipfile
from PIL import Image


def identifier_valid(value):
    return (isinstance(value, str) and 1 <= len(value) <= 64
            and all(c.isascii() and (c.isalnum() or c in "_-") for c in value))


def unchanged_gray8(image):
    if image.mode == "L":
        return True
    return (image.mode == "P" and "transparency" not in image.info
            and image.getpalette() == [v for v in range(256) for _ in range(3)])


def decode(source, descriptors):
    decoded = []
    identifiers = set()
    for item in descriptors:
        identifier = item["id"]
        if not identifier_valid(identifier) or identifier in identifiers:
            return [], "Public image identifiers must be unique and safe file names."
        identifiers.add(identifier)
        member = item.get("sourceMember", identifier + ".tif")
        if member not in source.namelist():
            return [], "A declared source member is missing."
        with Image.open(io.BytesIO(source.read(member))) as image:
            if image.size != (item["width"], item["height"]) or not unchanged_gray8(image):
                return [], "Unexpected public image geometry or pixel mode: " + identifier
            pixels = image.tobytes()
        if hashlib.sha256(pixels).hexdigest() != item["sha256"]:
            return [], "Decoded samples differ from the frozen manifest: " + identifier
        decoded.append((identifier, pixels))
    return decoded, ""


def verify_archive(archive, manifest):
    if manifest["resolutionPpi"] != 500:
        return [], "Only verified nominal 500 ppi cohorts are supported."
    if hashlib.sha256(archive).hexdigest() != manifest["archiveSha256"]:
        return [], "Archive differs from the frozen official download."
    descriptors = manifest["images"]
    selected = {item.get("sourceMember", item["id"] + ".tif") for item in descriptors}
    expected = manifest.get("archiveMembers", sorted(selected))
    if not descriptors or len(expected) != len(set(expected)) or not selected <= set(expected):
        return [], "The selected images differ from declared archive membership."
    with zipfile.ZipFile(io.BytesIO(archive)) as source:
        actual = source.namelist()
        if len(actual) != len(expected) or set(actual) != set(expected):
            return [], "Archive members differ from the frozen cohort."
        return decode(source, descriptors)


def prepare(archive_path: Path, output: Path, manifest_path: Path) -> int:
    try:
        manifest = json.loads(manifest_path.read_text())
        decoded, error = verify_archive(archive_path.read_bytes(), manifest)
    except (OSError, ValueError, KeyError, TypeError, zipfile.BadZipFile) as error:
        print("Cannot decode pinned public images: " + str(error), file=sys.stderr)
        return 1
    if error:
        print(error, file=sys.stderr)
        return 1
    output.mkdir(parents=True, exist_ok=True, mode=0o700)
    for identifier, pixels in decoded:
        (output / (identifier + ".gray8")).write_bytes(pixels)
    print(f"Prepared {len(decoded)} verified public images. No resampling or enhancement applied.")
    return 0
