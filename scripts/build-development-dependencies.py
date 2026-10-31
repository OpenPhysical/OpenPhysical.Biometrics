#!/usr/bin/env python3
# SPDX-License-Identifier: GPL-2.0-or-later
"""Build algorithm packages from sibling source checkouts and staged native assets."""
import argparse
from pathlib import Path
import subprocess
import sys
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
WORKSPACE = ROOT.parent
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("--skip-native", action="store_true", help="Package native assets already staged by the runtime build jobs.")
parser.add_argument("--complete-runtimes", action="store_true", help="Require all five supported runtimes when packing release dependencies.")
options = parser.parse_args()
properties = ET.parse(ROOT / "Directory.Build.props").getroot()


def version(property_name):
    value = properties.findtext(".//" + property_name)
    if not value:
        raise SystemExit(f"Missing dependency version property: {property_name}")
    return value


def run(repository, *arguments):
    subprocess.run(arguments, cwd=WORKSPACE / repository, check=True)


def pack(repository, project, package_version, partial_property):
    arguments = ["dotnet", "pack", "src/" + project, "-c", "Release", "-m:1", "-nr:false", "-p:UseSharedCompilation=false", "-p:PackageVersion=" + package_version]
    if not options.complete_runtimes:
        arguments.append("-p:" + partial_property + "=true")
    run(repository, *arguments)


# Sanitizer and test-hook binaries remain separate from these release assets.
if not options.skip_native:
    run("OpenPhysical.Biometrics.NBIS", sys.executable, "scripts/build-nbis.py")
pack("OpenPhysical.Biometrics.NBIS", "OpenPhysical.Biometrics.NBIS", version("NbisPackageVersion"), "AllowPartialNbisRuntimes")
for algorithm in ("nfiq1", "nfiq2"):
    if not options.skip_native:
        run("OpenPhysical.Biometrics.NFIQ", sys.executable, "scripts/build-native.py", "--algorithm", algorithm)
    pack("OpenPhysical.Biometrics.NFIQ", "OpenPhysical.Biometrics." + algorithm.upper(), version("NfiqPackageVersion"), "AllowPartialQualityRuntimes")
