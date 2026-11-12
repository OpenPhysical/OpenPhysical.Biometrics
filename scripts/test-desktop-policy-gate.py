#!/usr/bin/env python3
# SPDX-License-Identifier: GPL-2.0-or-later
"""Exercise release blocking independently of the app publisher."""

import hashlib
import itertools
import json
from pathlib import Path
import runpy
import subprocess
import sys
import tempfile
import unittest

gate = runpy.run_path(str(Path(__file__).with_name("check-desktop-policy.py")))
validate = gate["validate"]


class DesktopPolicyGateTests(unittest.TestCase):
    def setUp(self):
        self.location = tempfile.TemporaryDirectory()
        self.addCleanup(self.location.cleanup)
        self.repo = Path(self.location.name)
        self.source = self.repo / "src/OpenPhysical.Biometrics.Desktop/processing.cs"
        self.source.parent.mkdir(parents=True)
        self.source.write_text("frozen source")
        self.report = self.repo / "report.json"
        images = [{"Id": name, "PersonId": person, "FingerId": finger}
                  for name, person, finger in (("a1", "p1", "a"), ("a2", "p1", "a"),
                                               ("b1", "p1", "b"), ("b2", "p1", "b"),
                                               ("c1", "p2", "c"), ("c2", "p2", "c"))]
        counts = {"GenuineAccepted": 2, "ImpostorAccepted": 0,
                  "BetweenPersonAccepted": 0, "WithinPersonDifferentFingerAccepted": 0,
                  "AvailableGenuinePairs": 3, "UnavailableGenuinePairs": 0,
                  "AvailableImpostorPairs": 12, "UnavailableImpostorPairs": 0,
                  "AvailableBetweenPersonPairs": 8, "UnavailableBetweenPersonPairs": 0,
                  "AvailableWithinPersonDifferentFingerPairs": 4,
                  "UnavailableWithinPersonDifferentFingerPairs": 0, "UnavailableImages": []}
        self.data = {
            "SchemaVersion": 2, "Protocol": "prospective-full-attempts-v2",
            "Passed": True, "Threshold": 40, "ImageCount": 6, "Images": images,
            "GenuinePairs": 3, "ImpostorPairs": 12,
            "BetweenPersonPairs": 8, "WithinPersonDifferentFingerPairs": 4,
            "OperationalErrors": [],
            "UnavailableOriginalImages": 0, "UnavailableProcessedImages": 0,
            "SourceHashes": {"src/OpenPhysical.Biometrics.Desktop/processing.cs": hashlib.sha256(self.source.read_bytes()).hexdigest()},
            "Original": json.loads(json.dumps(counts)), "Processed": json.loads(json.dumps(counts)),
            "Pairs": [{"LeftId": left["Id"], "RightId": right["Id"],
                       "OriginalScore": self.score(left, right),
                       "ProcessedScore": self.score(left, right)}
                      for left, right in itertools.combinations(images, 2)],
        }

    @staticmethod
    def score(left, right):
        if left["FingerId"] != right["FingerId"]:
            return 0
        return 10 if left["FingerId"] == "a" else 40

    def missing_processed(self):
        self.data["UnavailableProcessedImages"] = 1
        arm = self.data["Processed"]
        arm["UnavailableImages"] = [{"Id": "a1", "Reason": "ImageNotAssessable",
                                     "DiagnosticCode": "QualityImageNotAssessable"}]
        for name, available, unavailable in (("Genuine", 2, 1), ("Impostor", 8, 4),
                                             ("BetweenPerson", 6, 2),
                                             ("WithinPersonDifferentFinger", 2, 2)):
            arm["Available" + name + "Pairs"] = available
            arm["Unavailable" + name + "Pairs"] = unavailable
        for pair in self.data["Pairs"]:
            if "a1" in (pair["LeftId"], pair["RightId"]):
                pair["ProcessedScore"] = None

    def check(self):
        self.report.write_text(json.dumps(self.data))
        return validate(self.repo, self.report)

    def test_failed_or_missing_evidence_blocks_publication(self):
        self.assertTrue(validate(self.repo, self.report))
        self.data["Passed"] = False
        self.assertTrue(self.check())

    def test_true_flag_cannot_hide_regression_or_new_false_match(self):
        self.data["Processed"]["GenuineAccepted"] = 1
        self.assertTrue(self.check())
        self.data["Processed"]["GenuineAccepted"] = 2
        self.data["Processed"]["ImpostorAccepted"] = 1
        self.assertTrue(self.check())

    def test_changed_source_invalidates_previously_passed_evidence(self):
        self.source.write_text("changed source")
        self.assertTrue(self.check())

    def test_omitted_or_new_processing_source_invalidates_evidence(self):
        extra = self.source.with_name("new-policy.cs")
        extra.write_text("new processing")
        self.assertTrue(self.check())

    def test_hidden_unavailable_images_or_pairs_cannot_pass(self):
        self.data["UnavailableProcessedImages"] = 1
        self.assertTrue(self.check())
        self.data["UnavailableProcessedImages"] = 0
        self.data["Processed"]["AvailableGenuinePairs"] = 2
        self.assertTrue(self.check())

    def test_impossible_counts_and_empty_cohort_cannot_pass(self):
        self.data["Processed"]["GenuineAccepted"] = 4
        self.assertTrue(self.check())
        self.data["GenuinePairs"] = 0
        self.assertTrue(self.check())

    def test_malformed_report_blocks_with_a_message(self):
        self.report.write_text("{")
        self.assertTrue(validate(self.repo, self.report))

    def test_matching_current_source_and_counts_pass(self):
        self.assertEqual("", self.check())

    def test_recorded_image_inability_can_pass_without_dropping_any_attempt(self):
        self.missing_processed()
        self.assertEqual("", self.check())

    def test_recorded_inability_cannot_hide_a_net_genuine_loss(self):
        self.missing_processed()
        self.data["Processed"]["GenuineAccepted"] = 1
        self.assertTrue(self.check())

    def test_operational_failures_cannot_be_treated_as_image_inability(self):
        self.missing_processed()
        self.data["Processed"]["UnavailableImages"][0]["DiagnosticCode"] = "EngineFailed"
        self.assertTrue(self.check())
        self.data["OperationalErrors"] = [{"Code": "QualityEngineFailed"}]
        self.assertTrue(self.check())

    def test_unknown_or_duplicate_unavailable_images_cannot_pass(self):
        self.missing_processed()
        self.data["Processed"]["UnavailableImages"][0]["Id"] = "unknown"
        self.assertTrue(self.check())
        self.data["Processed"]["UnavailableImages"][0]["Id"] = "a1"
        self.data["Processed"]["UnavailableImages"] *= 2
        self.assertTrue(self.check())

    def test_missing_pair_scores_must_be_explicit_null_not_fabricated_zero(self):
        self.missing_processed()
        pair = next(pair for pair in self.data["Pairs"] if pair["ProcessedScore"] is None)
        pair["ProcessedScore"] = 0
        self.assertTrue(self.check())
        del pair["ProcessedScore"]
        self.assertTrue(self.check())

    def test_dropped_duplicate_or_unknown_pairs_cannot_pass(self):
        original = json.loads(json.dumps(self.data["Pairs"]))
        self.data["Pairs"].pop()
        self.assertTrue(self.check())
        self.data["Pairs"] = original[:-1] + [original[0]]
        self.assertTrue(self.check())
        self.data["Pairs"] = original
        self.data["Pairs"][0]["LeftId"] = "unknown"
        self.assertTrue(self.check())

    def test_pair_categories_and_image_count_must_reconcile_with_identities(self):
        self.data["BetweenPersonPairs"] = 9
        self.assertTrue(self.check())
        self.data["BetweenPersonPairs"] = 8
        self.data["ImageCount"] = 5
        self.assertTrue(self.check())

    def test_acceptance_counts_are_derived_from_scores_and_bounded_by_available_pairs(self):
        self.data["Pairs"][0]["ProcessedScore"] = 40
        self.assertTrue(self.check())
        self.data["Processed"]["GenuineAccepted"] = 3
        self.assertEqual("", self.check())

    def test_nonfinite_fractional_and_boolean_counts_or_scores_cannot_pass(self):
        for bad in (float("nan"), float("inf"), 2.5, True, -1):
            with self.subTest(bad=bad):
                self.data["Processed"]["GenuineAccepted"] = bad
                self.assertTrue(self.check())
        self.data["Processed"]["GenuineAccepted"] = 2
        for bad in (float("nan"), float("inf"), 2.5, True, -1):
            self.data["Pairs"][0]["ProcessedScore"] = bad
            self.assertTrue(self.check())

    def test_baseline_must_have_a_real_genuine_accept_and_available_nonmates(self):
        for pair in self.data["Pairs"]:
            pair["OriginalScore"] = 0
        self.data["Original"]["GenuineAccepted"] = 0
        self.assertTrue(self.check())

    def test_old_schema_cannot_be_reinterpreted_as_a_new_passing_protocol(self):
        self.data["SchemaVersion"] = 1
        self.assertTrue(self.check())

    def test_research_record_is_deterministic_and_contains_no_fabricated_results(self):
        writer = gate.get("write_research_record")
        self.assertIsNotNone(writer)
        self.assertEqual("", writer(self.repo, self.report))
        first = self.report.read_bytes()
        self.assertEqual("", writer(self.repo, self.report))
        self.assertEqual(first, self.report.read_bytes())
        record = json.loads(first)
        self.assertIs(record["Passed"], False)
        self.assertEqual("prospective-full-attempts-v2", record["Protocol"])
        self.assertEqual(self.data["SourceHashes"], record["SourceHashes"])
        self.assertNotIn("Pairs", record)
        self.assertNotIn("Original", record)
        self.assertTrue(validate(self.repo, self.report))

    def test_research_record_never_overwrites_the_historical_failure(self):
        history = self.repo / "docs/validation/live-image-policy-v1.json"
        history.parent.mkdir(parents=True)
        history.write_text('{"SchemaVersion":1,"Passed":false}')
        before = history.read_bytes()
        self.assertTrue(gate["write_research_record"](self.repo, history))
        self.assertEqual(before, history.read_bytes())

    def test_research_record_cli_writes_only_nonpassing_current_source_evidence(self):
        result = subprocess.run([sys.executable, str(Path(__file__).with_name("check-desktop-policy.py")),
                                 "--research-record", str(self.report)],
                                capture_output=True, text=True, check=False)
        self.assertEqual(0, result.returncode, result.stderr)
        record = json.loads(self.report.read_text())
        self.assertIs(record["Passed"], False)
        self.assertEqual(2, record["SchemaVersion"])
        self.assertIn("publication remains blocked", result.stdout)
        self.assertNotIn("Pairs", record)


if __name__ == "__main__":
    unittest.main()
