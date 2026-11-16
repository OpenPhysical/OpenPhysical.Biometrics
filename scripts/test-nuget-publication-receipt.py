#!/usr/bin/env python3
"""Offline checks for the publication receipt's package identity gates."""

import importlib.util
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch
from types import SimpleNamespace
import zipfile
from urllib.error import HTTPError


SCRIPT = Path(__file__).with_name("verify-nuget-publication-receipt.py")
sys.path.insert(0, str(SCRIPT.parent))
SPEC = importlib.util.spec_from_file_location("nuget_publication_receipt", SCRIPT)
receipt = importlib.util.module_from_spec(SPEC)
assert SPEC.loader is not None
SPEC.loader.exec_module(receipt)


class PublicationReceiptTests(unittest.TestCase):
    def test_preflight_accepts_absence_but_rejects_a_public_or_unrelated_package(self):
        with patch.object(receipt, "request_json", side_effect=HTTPError("https://api.github.com", 404, "missing", {}, None)):
            receipt.check_existing_package("OpenPhysical.Biometrics.TestFixtures", "synthetic-test-token")
        for metadata in (
            {"visibility": "public", "repository": {"full_name": "OpenPhysical/OpenPhysical.Biometrics"}},
            {"visibility": "private", "repository": {"full_name": "OpenPhysical/Other"}},
        ):
            with patch.object(receipt, "request_json", return_value=metadata), self.assertRaises(ValueError):
                receipt.check_existing_package("OpenPhysical.Biometrics.TestFixtures", "synthetic-test-token")
        with patch.object(receipt, "request_json", side_effect=HTTPError("https://api.github.com", 403, "denied", {}, None)):
            with self.assertRaises(HTTPError):
                receipt.check_existing_package("OpenPhysical.Biometrics.TestFixtures", "synthetic-test-token")

    def test_restore_requires_the_exact_archive_and_keeps_credentials_out_of_arguments(self):
        with tempfile.TemporaryDirectory() as temp:
            workspace = Path(temp)
            project, config = receipt.write_consumer(
                workspace, "OpenPhysical.Biometrics.TestFixtures", "1.0.0-dev.20261116.1",
            )
            with patch.object(receipt.subprocess, "run", return_value=SimpleNamespace(returncode=0)) as restore:
                with self.assertRaisesRegex(ValueError, "expected package archive"):
                    receipt.restore_package(project, config, workspace, "fixture-user", "synthetic-test-token")
                archive = workspace / "packages" / receipt.package_package_dir(project) / "openphysical.biometrics.testfixtures.1.0.0-dev.20261116.1.nupkg"
                archive.parent.mkdir(parents=True)
                archive.write_bytes(b"synthetic archive")
                self.assertEqual(receipt.restore_package(project, config, workspace, "fixture-user", "synthetic-test-token"), archive)
                arguments, options = restore.call_args
                self.assertNotIn("synthetic-test-token", " ".join(arguments[0]))
                self.assertIn("--no-http-cache", arguments[0])
                self.assertEqual(options["env"]["NUGET_PACKAGES"], str(workspace / "packages"))
                self.assertEqual(options["env"]["NuGetPackageSourceCredentials_openphysical"],
                                 "Username=fixture-user;Password=synthetic-test-token")

    def test_only_private_package_linked_to_owner_is_accepted(self):
        metadata = {"visibility": "private", "repository": {"full_name": "OpenPhysical/OpenPhysical.Biometrics"}}
        self.assertEqual(receipt.validate_package_metadata(metadata), {
            "visibility": "private", "repository": "OpenPhysical/OpenPhysical.Biometrics",
        })
        for changed in (
            {"visibility": "public", "repository": metadata["repository"]},
            {"visibility": "private", "repository": {"full_name": "OpenPhysical/Other"}},
            {"visibility": "private", "repository": None},
        ):
            with self.subTest(changed=changed), self.assertRaises(ValueError):
                receipt.validate_package_metadata(changed)

    def test_temporary_consumer_is_exactly_mapped_to_github_packages(self):
        with tempfile.TemporaryDirectory() as temp:
            project, config = receipt.write_consumer(
                Path(temp), "OpenPhysical.Biometrics.TestFixtures", "1.0.0-dev.20261116.1",
            )
            project_text = project.read_text(encoding="utf-8")
            config_text = config.read_text(encoding="utf-8")
            self.assertIn('Version="1.0.0-dev.20261116.1"', project_text)
            self.assertIn("https://nuget.pkg.github.com/OpenPhysical/index.json", config_text)
            self.assertIn('<package pattern="OpenPhysical.Biometrics.TestFixtures" />', config_text)
            self.assertNotIn("Password", config_text)
            self.assertEqual(receipt.package_package_dir(project).as_posix(),
                             "openphysical.biometrics.testfixtures/1.0.0-dev.20261116.1")

    def test_downloaded_archive_must_match_the_source_manifest_byte_for_byte(self):
        package_id = "OpenPhysical.Biometrics.TestFixtures"
        version = "1.0.0-dev.20261116.1"
        commit = "a" * 40
        nuspec = (
            "<?xml version=\"1.0\"?><package><metadata><id>"
            + package_id
            + "</id><version>"
            + version
            + "</version><repository type=\"git\" commit=\""
            + commit
            + "\" /></metadata></package>"
        )
        with tempfile.TemporaryDirectory() as temp:
            archive = Path(temp) / f"{package_id}.{version}.nupkg"
            with zipfile.ZipFile(archive, "w") as package:
                package.writestr(f"{package_id}.nuspec", nuspec)
                package.writestr("content/fixture.bin", b"fixture bytes")
            expected = receipt.inspect_package(archive, package_id, version, commit)
            self.assertEqual(receipt.validate_downloaded_archive(archive, expected, commit),
                             (expected["size"], expected["sha256"]))
            with zipfile.ZipFile(archive, "w") as package:
                package.writestr(f"{package_id}.nuspec", nuspec)
                package.writestr("content/fixture.bin", b"changed fixture bytes")
            with self.assertRaisesRegex(ValueError, "differs from the source-bound"):
                receipt.validate_downloaded_archive(archive, expected, commit)


if __name__ == "__main__":
    unittest.main()
