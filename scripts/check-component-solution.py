#!/usr/bin/env python3
# SPDX-License-Identifier: GPL-2.0-or-later
"""Keep every non-desktop component in standalone CI and reject the Net-dependent GUI closure."""
import json
import re
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
DESKTOP = {"OpenPhysical.Biometrics.Desktop", "OpenPhysical.Biometrics.Desktop.Tests", "OpenPhysical.Biometrics.Studio"}


def check():
    solution = ROOT / "OpenPhysical.Biometrics.sln"
    paths = re.findall(r'^Project\([^\n]+ = "[^\"]+", "([^\"]+\.csproj)"', solution.read_text(encoding="utf-8-sig"), re.M)
    expected = {path.replace("\\", "/") for path in paths if Path(path.replace("\\", "/")).stem not in DESKTOP}
    selected = json.loads((ROOT / "OpenPhysical.Biometrics.Components.slnf").read_text())["solution"]
    actual = {path.replace("\\", "/") for path in selected["projects"]}
    if selected["path"] != solution.name or actual != expected or len(actual) != len(selected["projects"]):
        raise ValueError("Component filter must contain every solution project except the three desktop integration projects.")
    pending, seen = [ROOT / path for path in actual], set()
    while pending:
        path = pending.pop().resolve()
        if path in seen:
            continue
        seen.add(path)
        if not path.is_relative_to(ROOT) or path.stem in DESKTOP:
            raise ValueError("Component CI must not require the Net-dependent desktop integration: " + str(path))
        pending.extend(path.parent / node.attrib["Include"].replace("\\", "/")
                       for node in ET.parse(path).getroot().iter("ProjectReference"))
    print(f"Component filter retains {len(actual)} projects and every non-desktop test project without a Net checkout.")


if __name__ == "__main__":
    try:
        check()
    except (ValueError, ET.ParseError) as error:
        print(error, file=sys.stderr)
        sys.exit(1)
