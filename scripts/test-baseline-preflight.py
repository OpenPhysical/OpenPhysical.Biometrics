#!/usr/bin/env python3
# SPDX-License-Identifier: GPL-2.0-or-later
"""Synthetic final-freeze checks, with no cohort or native analyzer access."""
import copy
import hashlib
import importlib.util
import io
import json
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch
import zipfile

sys.dont_write_bytecode = True
scripts = Path(__file__).resolve().parent
sys.path.insert(0, str(scripts))
from baseline_preflight import preflight, runner_paths


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


class BaselinePreflightTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.repo = self.root / "repo"
        self.repo.mkdir()
        for name in ("prepare-fvc-baseline.py", "baseline_preflight.py", "check-desktop-policy.py"):
            self.write(self.repo / "scripts" / name, name)
        self.source = self.repo / "src/OpenPhysical.Biometrics.Desktop/operator.cs"
        self.write(self.source, "current product")
        base = self.repo / "tests/OpenPhysical.Biometrics.Desktop.Tests"
        names = ["Validation/Prospective/Runner.cs", "Validation/FvcBaseline.cs"]
        names += ["Processing/" + name + ".cs" for name in (
            "CountingFingerprintEngine", "CountingQualityProvider", "CountingUnifiedQualityProvider",
            "ProcessingFrameFixtures", "ScoringTestServices")]
        names += ["Storage/StorageFixtures.cs"]
        self.copies = self.root / "consumer/Tests"
        for name in names:
            self.write(base / name, name)
            self.write(self.copies / name, name)
        self.project = self.root / "consumer/consumer.csproj"
        self.write(self.project, "frozen consumer project")
        self.binary = self.root / "consumer/bin/net10.0"
        self.write(self.binary / "consumer.dll", "frozen executable")
        self.write(self.binary / "runtime/osx-arm64/libengine.dylib", "native engine")
        self.test_receipt = self.root / "tests.trx"
        self.write(self.test_receipt, "actual passing test receipt")
        self.raw_folder = self.root / "raw"
        self.write(self.raw_folder / "a.gray8", bytes(range(12)))
        self.write(self.raw_folder / "b.gray8", bytes(reversed(range(12))))
        self.snapshot = self.root / "historical.zip"
        with zipfile.ZipFile(self.snapshot, "w") as archive:
            archive.writestr("Analyzer.cs", "frozen analyzer")
        member_sha = hashlib.sha256(b"frozen analyzer").hexdigest()
        self.raw = {"schemaVersion": 1, "role": "final", "resolutionPpi": 500,
                    "people": [{"id": "p1"}], "legacyAnalyzerSnapshotFile": "relocated.zip",
                    "legacyAnalyzerSnapshotSha256": sha(self.snapshot),
                    "legacyAnalyzerSourceFiles": {"Analyzer.cs": member_sha},
                    "images": [{"id": name, "personId": "p1", "fingerId": "p1:1",
                                "width": 4, "height": 3,
                                "sha256": sha(self.raw_folder / (name + ".gray8"))}
                               for name in ("a", "b")]}
        self.raw_path = self.root / "raw-manifest.json"
        self.dump(self.raw_path, self.raw)
        self.receipt = {"SchemaVersion": 1, "Purpose": "chosen-policy-final-preflight",
                        "Role": "final", "FinalAuthorized": True, "ChosenCandidate": "RawHull0",
                        "RawManifest": self.identity(self.raw_path),
                        "SourceHashes": {self.source.relative_to(self.repo).as_posix(): sha(self.source)},
                        "RunnerSourceHashes": {name: sha(self.repo / name) for name in runner_paths(self.repo)},
                        "RunnerSourceRoot": str(self.copies), "RunnerBuildProject": self.identity(self.project),
                        "ExecutionRoot": str(self.binary),
                        "ExecutionHashes": [self.identity(path) for path in self.binary.rglob("*") if path.is_file()],
                        "Policy": {"AutomaticEnhancement": False, "Brightness": 0, "Contrast": 0,
                                   "TileSize": 32, "ClipLimit": 2, "Blend": 0, "Margin": 32,
                                   "ConfiguredBoundaryGuard": 8, "EffectiveBoundaryGuard": 0, "Threshold": 40},
                        "HistoricalOracle": {"Script": self.identity(self.repo / "scripts/prepare-fvc-baseline.py"),
                                             "Helper": self.identity(self.repo / "scripts/baseline_preflight.py"),
                                             "PolicyGate": self.identity(self.repo / "scripts/check-desktop-policy.py"),
                                             "Snapshot": self.identity(self.snapshot),
                                             "SourceHashes": {"Analyzer.cs": member_sha}},
                        "Tests": {"Command": "synthetic test command", "Passed": True,
                                  "ReceiptPath": str(self.test_receipt), "ReceiptSha256": sha(self.test_receipt)}}
        self.receipt_path = self.root / "freeze.json"
        self.derived = copy.deepcopy(self.raw)
        self.derived_path = self.root / "derived.json"
        self.save()

    @staticmethod
    def write(path, value):
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(value if isinstance(value, bytes) else value.encode())

    @staticmethod
    def dump(path, value):
        path.write_text(json.dumps(value))

    @staticmethod
    def identity(path):
        return {"Path": str(path), "Sha256": sha(path)}

    def save(self):
        self.dump(self.receipt_path, self.receipt)
        self.derived["frozenPolicySha256"] = sha(self.receipt_path)
        self.derived["rawManifestSha256"] = sha(self.raw_path)
        self.dump(self.derived_path, self.derived)

    def check(self, freeze=True):
        return preflight(self.repo, self.raw_folder, self.derived_path,
                         self.receipt_path if freeze else None)

    def test_complete_receipt_uses_verified_absolute_snapshot_without_changing_raw_manifest(self):
        before = self.raw_path.read_bytes()
        manifest, snapshot, error = self.check()
        self.assertEqual("", error)
        self.assertEqual(self.snapshot, snapshot)
        self.assertEqual(self.derived, manifest)
        self.assertEqual(before, self.raw_path.read_bytes())

    def test_hash_shaped_string_does_not_replace_an_actual_receipt(self):
        self.assertTrue(self.check(freeze=False)[2])
        self.receipt_path.write_text("{}")
        self.assertTrue(self.check()[2])

    def test_every_raw_image_hash_and_geometry_are_checked_in_development_too(self):
        self.derived["role"] = "development"
        self.save()
        self.write(self.raw_folder / "b.gray8", b"wrong bytes!!")
        self.assertTrue(self.check(freeze=False)[2])
        self.derived["images"][1]["sha256"] = sha(self.raw_folder / "b.gray8")
        self.derived["images"][1]["width"] = 801
        self.save()
        self.assertTrue(self.check(freeze=False)[2])

    def test_safe_unique_image_identifiers_and_valid_integer_dimensions_are_required(self):
        for key, value in (("id", "../escape"), ("id", "a"), ("width", True),
                           ("width", 0), ("height", 751)):
            with self.subTest(key=key, value=value):
                modified = copy.deepcopy(self.derived)
                modified["images"][1][key] = value
                self.dump(self.derived_path, modified)
                self.assertTrue(self.check()[2])

    def test_descriptor_or_person_changes_cannot_be_hidden_in_the_derived_manifest(self):
        for target, key, value in ((self.derived["images"][0], "personId", "other"),
                                   (self.derived, "people", [{"id": "other"}])):
            previous = copy.deepcopy(target[key])
            target[key] = value
            self.save()
            self.assertTrue(self.check()[2])
            target[key] = previous
        self.derived["images"][0].update(legacyMaskSha256="a" * 64, legacyPresent=True,
                                         legacyComponentCount=1, legacyIssue="None",
                                         paddedOriginalSha256="b" * 64)
        self.save()
        self.assertEqual("", self.check()[2])

    def test_current_product_runner_copy_and_execution_sets_must_be_complete_and_unchanged(self):
        mutations = [lambda: self.write(self.source, "changed"),
                     lambda: self.write(self.source.with_name("added.cs"), "new source"),
                     lambda: self.receipt["RunnerSourceHashes"].pop(next(iter(self.receipt["RunnerSourceHashes"]))),
                     lambda: self.write(self.copies / "Validation/Prospective/Runner.cs", "changed copy"),
                     lambda: self.write(self.binary / "added.dll", "new executable"),
                     lambda: self.write(self.binary / "consumer.dll", "changed executable")]
        for mutate in mutations:
            with self.subTest(mutation=mutate):
                receipt_before = copy.deepcopy(self.receipt)
                paths = {p: p.read_bytes() for p in self.root.rglob("*") if p.is_file()}
                mutate()
                self.save()
                self.assertTrue(self.check()[2])
                for p in self.root.rglob("*"):
                    if p.is_file() and p not in paths:
                        p.unlink()
                for p, data in paths.items():
                    p.write_bytes(data)
                self.receipt = receipt_before

    def test_failed_tests_changed_test_receipt_or_nonchosen_policy_block_final(self):
        for field, bad in (("Passed", False), ("ReceiptSha256", "0" * 64)):
            original = self.receipt["Tests"][field]
            self.receipt["Tests"][field] = bad
            self.save()
            self.assertTrue(self.check()[2])
            self.receipt["Tests"][field] = original
        self.receipt["Policy"]["Threshold"] = 41
        self.save()
        self.assertTrue(self.check()[2])

    def test_changed_historical_script_helper_snapshot_or_members_blocks(self):
        for key in ("Script", "Helper", "PolicyGate", "Snapshot"):
            previous = self.receipt["HistoricalOracle"][key]["Sha256"]
            self.receipt["HistoricalOracle"][key]["Sha256"] = "0" * 64
            self.save()
            self.assertTrue(self.check()[2])
            self.receipt["HistoricalOracle"][key]["Sha256"] = previous
        self.receipt["HistoricalOracle"]["SourceHashes"]["Analyzer.cs"] = "0" * 64
        self.save()
        self.assertTrue(self.check()[2])

    def test_invalid_input_returns_before_building_or_running_analyzer(self):
        spec = importlib.util.spec_from_file_location("baseline_command", scripts / "prepare-fvc-baseline.py")
        command = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(command)
        self.derived["role"] = "development"
        self.derived["legacyAnalyzerSnapshotFile"] = str(self.snapshot)
        self.save()
        self.write(self.raw_folder / "b.gray8", b"corrupted")
        stderr = io.StringIO()
        with patch.object(command.subprocess, "run") as run, patch.object(sys, "stderr", stderr):
            self.assertEqual(1, command.prepare(self.raw_folder, self.derived_path, True))
            run.assert_not_called()
        self.assertIn("Raw image samples or dimensions differ", stderr.getvalue())

    def test_receipt_does_not_accept_a_changed_original_raw_manifest(self):
        self.raw["people"] = [{"id": "changed"}]
        self.dump(self.raw_path, self.raw)
        self.assertIn("Original raw manifest identity", self.check()[2])

    def test_snapshot_cannot_extract_undeclared_or_escaped_source_members(self):
        self.derived["role"] = "development"
        self.derived["legacyAnalyzerSnapshotFile"] = str(self.snapshot)
        with zipfile.ZipFile(self.snapshot, "a") as archive:
            archive.writestr("../escape.cs", "unverified source")
        self.derived["legacyAnalyzerSnapshotSha256"] = sha(self.snapshot)
        self.save()
        self.assertIn("source member mismatch", self.check(freeze=False)[2])


if __name__ == "__main__":
    unittest.main()
