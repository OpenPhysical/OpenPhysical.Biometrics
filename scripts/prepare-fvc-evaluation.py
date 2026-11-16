#!/usr/bin/env python3
# SPDX-License-Identifier: GPL-2.0-or-later
"""Verify an official public optical500ppi FVC archive and decode its pinned images.

Download the public archive from the manifest's source URL first. This script
does not accept licenses, submit data, or redistribute the external image set.
Requires Pillow. Usage: prepare-fvc-evaluation.py ARCHIVE.zip OUTPUT_DIRECTORY
"""

import argparse
from pathlib import Path
import sys
from evaluation_image_decode import prepare

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("archive", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--manifest", type=Path, default=Path(__file__).resolve().parents[1] /
                        "tests/OpenPhysical.Biometrics.Desktop.Tests/Fixtures/fvc2002/manifest.json")
    args = parser.parse_args()
    sys.exit(prepare(args.archive, args.output, args.manifest.resolve()))
