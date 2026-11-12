# SPDX-License-Identifier: GPL-2.0-or-later
"""Verify frozen inputs before the historical baseline analyzer can run."""
import hashlib
import json
from pathlib import Path
import re
import runpy
import zipfile

source_paths = runpy.run_path(str(Path(__file__).with_name("check-desktop-policy.py")))["source_paths"]
BASELINE_FIELDS = {"legacyMaskSha256", "legacyPresent", "legacyComponentCount",
                   "legacyIssue", "paddedOriginalSha256"}
POLICY = {"AutomaticEnhancement": False, "Brightness": 0, "Contrast": 0,
          "TileSize": 32, "ClipLimit": 2, "Blend": 0, "Margin": 32,
          "ConfiguredBoundaryGuard": 8, "EffectiveBoundaryGuard": 0, "Threshold": 40}
TEST_PROJECT = "tests/OpenPhysical.Biometrics.Desktop.Tests"


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def digest_valid(value):
    return isinstance(value, str) and re.fullmatch(r"[0-9a-f]{64}", value) is not None


def file_identity(value, expected_path=None):
    if not isinstance(value, dict) or not isinstance(value.get("Path"), str):
        return False
    path = Path(value["Path"])
    return (path.is_absolute() and path.is_file() and digest_valid(value.get("Sha256"))
            and (expected_path is None or path.resolve() == expected_path.resolve())
            and sha(path) == value["Sha256"])


def runner_paths(repo):
    base = repo / TEST_PROJECT
    paths = set((base / "Validation/Prospective").glob("*.cs"))
    names = ("Validation/FvcBaseline.cs", "Storage/StorageFixtures.cs")
    names += tuple("Processing/" + name + ".cs" for name in (
        "CountingFingerprintEngine", "CountingQualityProvider", "CountingUnifiedQualityProvider",
        "ProcessingFrameFixtures", "ScoringTestServices"))
    paths.update(base / name for name in names)
    return {path.relative_to(repo).as_posix() for path in paths}


def execution_paths(root):
    return {path.resolve() for path in root.rglob("*") if path.is_file()
            and (path.suffix == ".dll" or path.name.endswith((".deps.json", ".runtimeconfig.json"))
                 or path.suffix == ".dylib" and (path.parent == root or "osx-arm64" in path.parts))}


def verify_source_and_execution(repo, receipt):
    hashes = receipt.get("SourceHashes")
    if not isinstance(hashes, dict) or set(hashes) != source_paths(repo) or not hashes:
        return "Policy freeze must bind the complete current product source set."
    if any(not digest_valid(digest) or not (repo / name).is_file()
           or sha(repo / name) != digest for name, digest in hashes.items()):
        return "Policy freeze product source hash mismatch."
    runner = receipt.get("RunnerSourceHashes")
    copy_root = Path(receipt.get("RunnerSourceRoot", ""))
    if (not isinstance(runner, dict) or set(runner) != runner_paths(repo)
            or not copy_root.is_absolute() or not copy_root.is_dir()
            or not file_identity(receipt.get("RunnerBuildProject"))):
        return "Policy freeze must bind the complete runner source and build project."
    expected_copies = {Path(name).relative_to(TEST_PROJECT).as_posix() for name in runner}
    actual_copies = {path.relative_to(copy_root).as_posix() for path in copy_root.rglob("*.cs")}
    if actual_copies != expected_copies:
        return "Policy freeze copied runner source set mismatch."
    for name, digest in runner.items():
        copied = copy_root / Path(name).relative_to(TEST_PROJECT)
        if (not digest_valid(digest) or not (repo / name).is_file()
                or sha(repo / name) != digest or sha(copied) != digest):
            return "Policy freeze runner source hash mismatch: " + name
    root = Path(receipt.get("ExecutionRoot", ""))
    files = receipt.get("ExecutionHashes")
    if not root.is_absolute() or not root.is_dir() or not isinstance(files, list) or not files:
        return "Policy freeze execution assets are missing."
    if any(not file_identity(item) for item in files):
        return "Policy freeze execution asset hash mismatch."
    listed = [Path(item["Path"]).resolve() for item in files]
    if len(listed) != len(set(listed)) or set(listed) != execution_paths(root):
        return "Policy freeze execution asset set mismatch."
    return ""


def without_baseline(manifest):
    result = {key: value for key, value in manifest.items()
              if key not in {"frozenPolicySha256", "rawManifestSha256", "images"}}
    result["images"] = [{key: value for key, value in item.items() if key not in BASELINE_FIELDS}
                        for item in manifest["images"]]
    return result


def verify_final(repo, manifest, receipt_path):
    if receipt_path is None or not receipt_path.is_file():
        return None, "Final cohort requires an actual --policy-freeze receipt."
    if not digest_valid(manifest.get("frozenPolicySha256")) or sha(receipt_path) != manifest["frozenPolicySha256"]:
        return None, "Final policy receipt hash mismatch."
    receipt = json.loads(receipt_path.read_text())
    if (receipt.get("SchemaVersion") != 1 or receipt.get("Purpose") != "chosen-policy-final-preflight"
            or receipt.get("Role") != "final" or receipt.get("FinalAuthorized") is not True
            or receipt.get("ChosenCandidate") != "RawHull0" or receipt.get("Policy") != POLICY
            or any(type(receipt["Policy"][key]) is not type(value) for key, value in POLICY.items())):
        return None, "Final receipt does not authorize the fixed RawHull0 policy at threshold 40."
    raw = receipt.get("RawManifest")
    if not file_identity(raw) or raw["Sha256"] != manifest.get("rawManifestSha256"):
        return None, "Original raw manifest identity mismatch."
    original = json.loads(Path(raw["Path"]).read_text())
    if original.get("role") != "final" or without_baseline(original) != without_baseline(manifest):
        return None, "Derived manifest changed raw cohort, image or person descriptors."
    tests = receipt.get("Tests", {})
    test_file = {"Path": tests.get("ReceiptPath"), "Sha256": tests.get("ReceiptSha256")}
    if (tests.get("Passed") is not True or not isinstance(tests.get("Command"), str)
            or not tests["Command"].strip() or not file_identity(test_file)):
        return None, "Final policy requires a verified passing test receipt."
    error = verify_source_and_execution(repo, receipt)
    if error:
        return None, error
    oracle = receipt.get("HistoricalOracle", {})
    for key, name in (("Script", "prepare-fvc-baseline.py"), ("Helper", "baseline_preflight.py"),
                      ("PolicyGate", "check-desktop-policy.py")):
        if not file_identity(oracle.get(key), repo / "scripts" / name):
            return None, "Historical oracle tooling identity mismatch: " + key
    if (not file_identity(oracle.get("Snapshot"))
            or oracle["Snapshot"]["Sha256"] != manifest.get("legacyAnalyzerSnapshotSha256")
            or oracle.get("SourceHashes") != manifest.get("legacyAnalyzerSourceFiles")):
        return None, "Historical oracle snapshot identity mismatch."
    return Path(oracle["Snapshot"]["Path"]), ""


def verify_snapshot(snapshot, manifest):
    if not snapshot.is_file() or sha(snapshot) != manifest.get("legacyAnalyzerSnapshotSha256"):
        return "Historical analyzer snapshot hash mismatch."
    expected = manifest.get("legacyAnalyzerSourceFiles")
    if not isinstance(expected, dict) or not expected:
        return "Historical analyzer member identities are missing."
    with zipfile.ZipFile(snapshot) as archive:
        actual = archive.namelist()
        if len(actual) != len(expected) or set(actual) != set(expected):
            return "Historical analyzer source member mismatch."
        for name, digest in expected.items():
            if (not isinstance(name, str) or Path(name).name != name or name in {".", ".."}
                    or not digest_valid(digest) or hashlib.sha256(archive.read(name)).hexdigest() != digest):
                return "Historical analyzer source member hash or path mismatch."
    return ""


def verify_images(folder, manifest):
    images = manifest.get("images")
    if (manifest.get("resolutionPpi") != 500 or not isinstance(images, list) or not images):
        return "Expected a nonempty nominal 500 ppi image manifest."
    identifiers = set()
    for item in images:
        identifier = item.get("id")
        if (not isinstance(identifier, str) or not re.fullmatch(r"[A-Za-z0-9_-]{1,64}", identifier)
                or identifier in identifiers):
            return "Public image identifiers must be unique and safe file names."
        identifiers.add(identifier)
        width, height = item.get("width"), item.get("height")
        if type(width) is not int or type(height) is not int or not (1 <= width <= 800 and 1 <= height <= 750):
            return "Invalid declared public geometry: " + identifier
        path = folder / (identifier + ".gray8")
        if not path.is_file() or path.resolve().parent != folder.resolve():
            return "Missing or escaped raw image: " + identifier
        pixels = path.read_bytes()
        if (len(pixels) != width * height or not digest_valid(item.get("sha256"))
                or hashlib.sha256(pixels).hexdigest() != item["sha256"]):
            return "Raw image samples or dimensions differ from the frozen manifest: " + identifier
    return ""


def preflight(repo, raw_folder, manifest_path, policy_freeze=None):
    """Read and validate everything before allowing analyzer build or execution."""
    try:
        manifest = json.loads(manifest_path.read_text())
        if not isinstance(manifest, dict):
            return {}, None, "Image manifest must be an object."
        snapshot = (manifest_path.parent / manifest.get("legacyAnalyzerSnapshotFile", "legacy-analyzer.snapshot.zip")).resolve()
        if manifest.get("role") == "final":
            snapshot, error = verify_final(repo, manifest, policy_freeze)
            if error:
                return {}, None, error
        error = verify_snapshot(snapshot, manifest) or verify_images(raw_folder, manifest)
        return manifest, snapshot, error
    except (OSError, ValueError, KeyError, TypeError, AttributeError, zipfile.BadZipFile) as error:
        return {}, None, "Cannot verify baseline inputs: " + str(error)
