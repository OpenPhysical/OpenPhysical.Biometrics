#!/usr/bin/env python3
# SPDX-License-Identifier: GPL-2.0-or-later
"""Check lossless subset decoding without biometric fixtures."""
import hashlib
import io
import json
from pathlib import Path
import tempfile
import unittest
import zipfile
from PIL import Image
from evaluation_image_decode import prepare


class EvaluationImageDecodeTests(unittest.TestCase):
    def setUp(self):
        self.location = tempfile.TemporaryDirectory()
        self.addCleanup(self.location.cleanup)
        self.root = Path(self.location.name)
        pixels = bytes([12, 34, 56, 78, 90, 123])
        encoded = io.BytesIO()
        Image.frombytes("L", (3, 2), pixels).save(encoded, format="TIFF")
        self.archive = self.root / "images.zip"
        with zipfile.ZipFile(self.archive, "w") as source:
            source.writestr("012_001_01.tif", encoded.getvalue())
            source.writestr("013_001_01.tif", encoded.getvalue())
            source.writestr("readme.txt", "person/finger/scan")
        self.manifest = {
            "resolutionPpi": 500,
            "archiveSha256": hashlib.sha256(self.archive.read_bytes()).hexdigest(),
            "archiveMembers": ["012_001_01.tif", "013_001_01.tif", "readme.txt"],
            "images": [{"id": "012_001_01", "sourceMember": "012_001_01.tif",
                        "width": 3, "height": 2,
                        "sha256": hashlib.sha256(pixels).hexdigest()}],
        }
        self.path = self.root / "manifest.json"
        self.output = self.root / "decoded"
        self.pixels = pixels

    def run_prepare(self):
        self.path.write_text(json.dumps(self.manifest))
        return prepare(self.archive, self.output, self.path)

    def test_subset_preserves_exact_pixels_and_excludes_other_subject(self):
        self.assertEqual(0, self.run_prepare())
        self.assertEqual(["012_001_01.gray8"], [p.name for p in self.output.iterdir()])
        self.assertEqual(self.pixels, (self.output / "012_001_01.gray8").read_bytes())

    def test_changed_pixels_or_archive_members_produce_no_partial_output(self):
        self.manifest["images"][0]["sha256"] = "0" * 64
        self.assertEqual(1, self.run_prepare())
        self.assertFalse(self.output.exists())
        self.manifest["archiveMembers"].remove("readme.txt")
        self.assertEqual(1, self.run_prepare())

    def test_undeclared_member_or_unsafe_identifier_is_rejected(self):
        self.manifest["images"][0]["sourceMember"] = "unknown.tif"
        self.assertEqual(1, self.run_prepare())
        self.manifest["images"][0]["sourceMember"] = "012_001_01.tif"
        self.manifest["images"][0]["id"] = "../escape"
        self.assertEqual(1, self.run_prepare())

    def replace_with_palette(self, palette):
        image = Image.frombytes("P", (3, 2), self.pixels)
        image.putpalette(palette)
        encoded = io.BytesIO()
        image.save(encoded, format="TIFF")
        with zipfile.ZipFile(self.archive, "w") as source:
            for name in self.manifest["archiveMembers"]:
                source.writestr(name, "person/finger/scan" if name.endswith(".txt")
                                else encoded.getvalue())
        self.manifest["archiveSha256"] = hashlib.sha256(self.archive.read_bytes()).hexdigest()

    def test_identity_grayscale_palette_preserves_unchanged_intensity_indices(self):
        self.replace_with_palette([v for v in range(256) for _ in range(3)])
        self.assertEqual(0, self.run_prepare())
        self.assertEqual(self.pixels, (self.output / "012_001_01.gray8").read_bytes())

    def test_colored_or_nonidentity_grayscale_palettes_are_rejected(self):
        palette = [v for v in range(256) for _ in range(3)]
        palette[12 * 3] = 255
        self.replace_with_palette(palette)
        self.assertEqual(1, self.run_prepare())
        self.replace_with_palette([255 - v for v in range(256) for _ in range(3)])
        self.assertEqual(1, self.run_prepare())
        self.assertFalse(self.output.exists())

    def test_declared_geometry_is_not_silently_resampled(self):
        self.manifest["images"][0]["width"] = 2
        self.assertEqual(1, self.run_prepare())
        self.assertFalse(self.output.exists())

    def test_duplicate_output_identifiers_cannot_overwrite_a_scan(self):
        self.manifest["images"].append(dict(self.manifest["images"][0]))
        self.assertEqual(1, self.run_prepare())
        self.assertFalse(self.output.exists())

    def test_unverified_resolution_is_rejected(self):
        self.manifest["resolutionPpi"] = 1000
        self.assertEqual(1, self.run_prepare())


if __name__ == "__main__":
    unittest.main()
