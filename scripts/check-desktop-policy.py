#!/usr/bin/env python3
# SPDX-License-Identifier: GPL-2.0-or-later
"""Block normal app publication until the exact capture policy passes its matching gate."""

import argparse
import hashlib
import json
from pathlib import Path
import sys


POLICY_PROJECTS = (
    "OpenPhysical.Biometrics.Capture", "OpenPhysical.Biometrics.Capture.Futronic",
    "OpenPhysical.Biometrics.Fingerprints", "OpenPhysical.Biometrics.Fingerprints.Nfiq2",
    "OpenPhysical.Biometrics.Desktop", "OpenPhysical.Biometrics.Capture.Usb",
    "OpenPhysical.Biometrics.Formats", "OpenPhysical.Biometrics.Imaging",
)
PROTOCOL = "prospective-full-attempts-v2"
KINDS = ("Genuine", "Impostor", "BetweenPerson", "WithinPersonDifferentFinger")
IMAGE_REJECTIONS = {
    "ImageNotAssessable": {"QualityImageNotAssessable", "Capture.Nfiq1NotAssessable"},
    "ContactRejected": {"Capture.ContactTooLight", "Capture.UnclearContact",
                        "Capture.NoFinger", "Capture.MultipleFingers"},
    "NoReliableArea": {"Capture.NoReliableArea"},
    "AmbiguousTissueRegion": {"capture.AmbiguousTissueRegion"},
}


def source_paths(repo):
    paths = set()
    for project in POLICY_PROJECTS:
        root = repo / "src" / project
        for pattern in ("*.cs", "*.csproj", "packages.lock.json"):
            paths.update(path.relative_to(repo).as_posix() for path in root.rglob(pattern)
                         if not {"bin", "obj"}.intersection(path.relative_to(root).parts))
    for name in ("Directory.Build.props", "Directory.Packages.props", "global.json"):
        if (repo / name).is_file():
            paths.add(name)
    return paths


def integer(value, minimum=0):
    return type(value) is int and value >= minimum


def image_identities(data):
    items = data.get("Images")
    count = data.get("ImageCount")
    if not integer(count, 2) or not isinstance(items, list) or len(items) != count:
        return {}
    result = {}
    for item in items:
        if not isinstance(item, dict):
            return {}
        values = [item.get(key) for key in ("Id", "PersonId", "FingerId")]
        if not all(isinstance(value, str) and value.strip() for value in values):
            return {}
        identifier, person, finger = values
        if identifier in result:
            return {}
        result[identifier] = (person, finger)
    return result


def unavailable_images(data, role, images):
    arm = data.get(role)
    if not isinstance(arm, dict) or not isinstance(arm.get("UnavailableImages"), list):
        return None
    items = arm["UnavailableImages"]
    count = data.get("Unavailable" + role + "Images")
    if not integer(count) or count != len(items):
        return None
    result = set()
    for item in items:
        if not isinstance(item, dict):
            return None
        identifier, reason, code = (item.get(key) for key in ("Id", "Reason", "DiagnosticCode"))
        if (not isinstance(identifier, str) or identifier not in images or identifier in result
                or not isinstance(reason, str) or not isinstance(code, str)
                or code not in IMAGE_REJECTIONS.get(reason, set())):
            return None
        result.add(identifier)
    return result


def empty_counts():
    return {key: 0 for kind in KINDS
            for key in (kind + "Accepted", "Available" + kind + "Pairs",
                        "Unavailable" + kind + "Pairs")}


def pair_kinds(left, right):
    if left == right:
        return ("Genuine",)
    return ("Impostor", "BetweenPerson" if left[0] != right[0]
            else "WithinPersonDifferentFinger")


def score_pair(pair, role, missing, left, right, kinds, counts):
    name = role + "Score"
    if name not in pair:
        return False
    score = pair[name]
    unavailable = left in missing or right in missing
    if unavailable:
        if score is not None:
            return False
    elif not integer(score) or score > 2147483647:
        return False
    for kind in kinds:
        counts[("Unavailable" if unavailable else "Available") + kind + "Pairs"] += 1
        if not unavailable and score >= 40:
            counts[kind + "Accepted"] += 1
    return True


def count_ledger(data, images, unavailable):
    rows = data.get("Pairs")
    if not isinstance(rows, list) or len(rows) != len(images) * (len(images) - 1) // 2:
        return None
    totals = {kind: 0 for kind in KINDS}
    arms = {role: empty_counts() for role in ("Original", "Processed")}
    seen = set()
    for row in rows:
        if not isinstance(row, dict):
            return None
        left, right = row.get("LeftId"), row.get("RightId")
        if (not isinstance(left, str) or not isinstance(right, str)
                or left not in images or right not in images or left == right):
            return None
        key = tuple(sorted((left, right)))
        if key in seen:
            return None
        seen.add(key)
        kinds = pair_kinds(images[left], images[right])
        for kind in kinds:
            totals[kind] += 1
        for role in arms:
            if not score_pair(row, role, unavailable[role], left, right, kinds, arms[role]):
                return None
    return totals, arms


def counts_complete(data):
    if data.get("OperationalErrors") != []:
        return False
    images = image_identities(data)
    if not images:
        return False
    missing = {role: unavailable_images(data, role, images)
               for role in ("Original", "Processed")}
    if any(value is None for value in missing.values()):
        return False
    ledger = count_ledger(data, images, missing)
    if ledger is None:
        return False
    totals, arms = ledger
    for kind, expected in totals.items():
        value = data.get(kind + "Pairs")
        if not integer(value) or value != expected:
            return False
    for role, expected in arms.items():
        for key, count in expected.items():
            value = data[role].get(key)
            if not integer(value) or value != count:
                return False
    old = arms["Original"]
    return (totals["Genuine"] > 0 and totals["Impostor"] > 0
            and old["GenuineAccepted"] > 0 and old["AvailableImpostorPairs"] > 0)


def validate(repo, report):
    if not report.is_file():
        return "Capture policy validation is missing. Run the fixed public matching gate first."
    try:
        data = json.loads(report.read_text())
    except (OSError, ValueError):
        return "Capture policy validation cannot be read."
    if not isinstance(data, dict) or data.get("Passed") is not True:
        return "Capture policy failed matching validation. Normal app publication is blocked."
    if data.get("SchemaVersion") != 2 or data.get("Protocol") != PROTOCOL:
        return "Capture policy validation requires the prospective full-attempts-v2 protocol."
    hashes = data.get("SourceHashes")
    if (data.get("Threshold") != 40 or not isinstance(hashes, dict)
            or not hashes or set(hashes) != source_paths(repo)):
        return "Capture policy validation lacks the fixed threshold or complete source association."
    for relative, expected in hashes.items():
        path = repo / relative
        if not path.is_file() or hashlib.sha256(path.read_bytes()).hexdigest() != expected:
            return f"Capture policy validation is stale: {relative}"
    if not counts_complete(data):
        return "Capture policy validation has incomplete pair evidence, invalid availability or operational errors."
    old, new = data["Original"], data["Processed"]
    if (new["GenuineAccepted"] < old["GenuineAccepted"]
            or new["ImpostorAccepted"] > old["ImpostorAccepted"]):
        return "Capture policy acceptance counts do not pass the release criteria."
    return ""


def write_research_record(repo, output):
    try:
        if output.resolve() == (repo / "docs/validation/live-image-policy-v1.json").resolve():
            return "The historical schema1 failure record must remain unchanged."
        record = {"SchemaVersion": 2, "Protocol": PROTOCOL, "Passed": False, "Threshold": 40,
                  "Reason": "Research candidate only. No passing full-attempt matching validation is recorded.",
                  "SourceHashes": {relative: hashlib.sha256((repo / relative).read_bytes()).hexdigest()
                                   for relative in sorted(source_paths(repo))}}
        output.parent.mkdir(parents=True, exist_ok=True)
        output.write_text(json.dumps(record, indent=2, sort_keys=True) + "\n")
    except OSError:
        return "The research-only validation record could not be written."
    return ""


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--research-record", type=Path,
                        help="Write a source-bound, non-passing candidate record without scores.")
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    result = (write_research_record(root, args.research_record) if args.research_record
              else validate(root, root / "docs/validation/live-image-policy-v2.json"))
    if result:
        print(result, file=sys.stderr)
        return 1
    print("Research-only record written; publication remains blocked." if args.research_record
          else "Capture policy matching gate passed for the current source.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
