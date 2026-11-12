#!/usr/bin/env python3
# SPDX-License-Identifier: GPL-2.0-or-later
"""Verify and decode a frozen subject partition without enhancement or resampling."""
import argparse
from pathlib import Path
import sys
from evaluation_image_decode import prepare

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("archive", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--manifest", type=Path, required=True)
    args = parser.parse_args()
    sys.exit(prepare(args.archive, args.output, args.manifest.resolve()))
