#!/usr/bin/env python3
"""Verify GitHub Packages ownership and byte identity after an immutable push."""

import argparse
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import time
from urllib.error import HTTPError, URLError
from urllib.parse import quote
from urllib.request import Request, urlopen
import xml.etree.ElementTree as ET

_MANIFEST_SCRIPT = Path(__file__).with_name("write-nuget-publication-manifest.py")
_MANIFEST_SPEC = importlib.util.spec_from_file_location("nuget_publication_manifest", _MANIFEST_SCRIPT)
if _MANIFEST_SPEC is None or _MANIFEST_SPEC.loader is None:
    raise RuntimeError("could not load the canonical NuGet publication manifest inspector")
_MANIFEST_MODULE = importlib.util.module_from_spec(_MANIFEST_SPEC)
_MANIFEST_SPEC.loader.exec_module(_MANIFEST_MODULE)
inspect_package = _MANIFEST_MODULE.inspect_package


API_ROOT = "https://api.github.com/orgs/OpenPhysical/packages/nuget"
GITHUB_NUGET_FEED = "https://nuget.pkg.github.com/OpenPhysical/index.json"
EXPECTED_REPOSITORY = "OpenPhysical/OpenPhysical.Biometrics"
API_VERSION = "2026-03-10"


def request_json(url: str, token: str) -> dict | list:
    request = Request(url, headers={
        "Accept": "application/vnd.github+json",
        "Authorization": f"Bearer {token}",
        "X-GitHub-Api-Version": API_VERSION,
    })
    with urlopen(request, timeout=30) as response:
        return json.loads(response.read().decode("utf-8"))


def validate_package_metadata(metadata: object) -> dict:
    if not isinstance(metadata, dict):
        raise ValueError("GitHub package API returned an invalid package record")
    repository = metadata.get("repository")
    if metadata.get("visibility") != "private":
        raise ValueError("published fixture package is not private")
    if not isinstance(repository, dict) or repository.get("full_name") != EXPECTED_REPOSITORY:
        raise ValueError("published fixture package is not linked to its owning repository")
    return {"visibility": "private", "repository": EXPECTED_REPOSITORY}


def wait_for_package_metadata(package_id: str, token: str) -> dict:
    url = f"{API_ROOT}/{quote(package_id.casefold(), safe='')}"
    last_error = None
    for attempt in range(12):
        try:
            return validate_package_metadata(request_json(url, token))
        except HTTPError as error:
            if error.code != 404:
                raise
            last_error = "package metadata has not propagated yet"
        except (URLError, TimeoutError) as error:
            last_error = type(error).__name__
        if attempt < 11:
            time.sleep(5)
    raise ValueError(last_error or "package metadata was not available")


def check_existing_package(package_id: str, token: str) -> None:
    url = f"{API_ROOT}/{quote(package_id.casefold(), safe='')}"
    try:
        metadata = request_json(url, token)
    except HTTPError as error:
        if error.code == 404:
            return
        raise
    validate_package_metadata(metadata)


def write_consumer(workspace: Path, package_id: str, version: str) -> tuple[Path, Path]:
    project = workspace / "consumer" / "FixtureReceiptConsumer.csproj"
    project.parent.mkdir(parents=True)
    project.write_text(
        "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup>"
        "<TargetFramework>net10.0</TargetFramework><RestorePackagesWithLockFile>false</RestorePackagesWithLockFile>"
        "</PropertyGroup><ItemGroup><PackageReference Include=\""
        + package_id
        + "\" Version=\""
        + version
        + "\" /></ItemGroup></Project>\n",
        encoding="utf-8",
    )
    config = workspace / "NuGet.Config"
    config.write_text(
        "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n"
        "<configuration><packageSources><clear/><add key=\"openphysical\" value=\""
        + GITHUB_NUGET_FEED
        + "\" /></packageSources><packageSourceMapping><clear/><packageSource key=\"openphysical\"><package pattern=\""
        + package_id
        + "\" /></packageSource></packageSourceMapping></configuration>\n",
        encoding="utf-8",
    )
    return project, config


def restore_package(project: Path, config: Path, workspace: Path, actor: str, token: str) -> Path:
    package_cache = workspace / "packages"
    http_cache = workspace / "http-cache"
    env = os.environ.copy()
    env["NUGET_PACKAGES"] = str(package_cache)
    env["NUGET_HTTP_CACHE_PATH"] = str(http_cache)
    env["NuGetPackageSourceCredentials_openphysical"] = f"Username={actor};Password={token}"
    result = subprocess.run(
        ["dotnet", "restore", str(project), "--configfile", str(config), "--no-http-cache", "--packages", str(package_cache)],
        cwd=project.parent,
        env=env,
        stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT,
        text=True,
        check=False,
    )
    if result.returncode:
        # Restore output is intentionally not echoed: it can include feed diagnostics.
        raise ValueError("fresh authenticated NuGet restore failed")
    expanded = package_cache / package_package_dir(project)
    archive = expanded / f"{expanded.parent.name}.{expanded.name}.nupkg"
    if not archive.is_file():
        raise ValueError("NuGet restore completed without the expected package archive")
    return archive


def package_package_dir(project: Path) -> Path:
    root = ET.parse(project).getroot()
    reference = next((node for node in root.iter("PackageReference") if node.get("Include")), None)
    if reference is None:
        raise ValueError("temporary restore project has no package reference")
    package_id = reference.get("Include", "").casefold()
    version = reference.get("Version", "").casefold()
    if not package_id or not version:
        raise ValueError("temporary restore project has an incomplete package identity")
    return Path(package_id) / version


def sha256(path: Path) -> tuple[int, str]:
    digest = hashlib.sha256()
    size = 0
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            size += len(block)
            digest.update(block)
    return size, digest.hexdigest()


def validate_downloaded_archive(path: Path, expected: dict, source_commit: str) -> tuple[int, str]:
    package_id, version = expected["id"], expected["version"]
    size, digest = sha256(path)
    actual = inspect_package(path, package_id, version, source_commit)
    if size != expected.get("size") or digest != expected.get("sha256") or actual != expected:
        raise ValueError("fresh NuGet archive differs from the source-bound publication manifest")
    return size, digest


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--manifest", type=Path, required=True)
    parser.add_argument("--preflight", action="store_true")
    parser.add_argument("--workspace", type=Path)
    parser.add_argument("--receipt", type=Path)
    args = parser.parse_args()
    token = os.environ.get("GITHUB_TOKEN")
    actor = os.environ.get("GITHUB_ACTOR")
    if not token or not actor:
        parser.error("GITHUB_TOKEN and GITHUB_ACTOR are required in the environment")
    if not args.preflight and (args.workspace is None or args.receipt is None):
        parser.error("--workspace and --receipt are required after publication")
    try:
        manifest = json.loads(args.manifest.read_text(encoding="utf-8"))
        packages = manifest.get("packages")
        if manifest.get("repository") != "OpenPhysical.Biometrics" or not isinstance(packages, list) or len(packages) != 1:
            raise ValueError("source manifest must identify exactly one fixture package")
        expected = packages[0]
        package_id, version = expected.get("id"), expected.get("version")
        if package_id != "OpenPhysical.Biometrics.TestFixtures" or not isinstance(version, str):
            raise ValueError("source manifest package identity is not the approved fixture package")
        if args.preflight:
            check_existing_package(package_id, token)
            print("Existing package is absent or private and linked to its owning repository.")
            return 0
        if args.workspace.exists():
            raise ValueError("receipt workspace must be fresh and absent")
        args.workspace.mkdir(parents=True)
        package_metadata = wait_for_package_metadata(package_id, token)
        project, config = write_consumer(args.workspace, package_id, version)
        restored_archive = restore_package(project, config, args.workspace, actor, token)
        downloaded = args.workspace / expected["file"]
        # Verify the bytes used by the fresh restore itself, not a separate HTTP fetch.
        shutil.copyfile(restored_archive, downloaded)
        size, digest = validate_downloaded_archive(downloaded, expected, manifest["sourceCommit"])
        receipt = {
            "schemaVersion": 1,
            "package": package_id,
            "version": version,
            "visibility": package_metadata["visibility"],
            "repository": package_metadata["repository"],
            "sourceCommit": manifest["sourceCommit"],
            "archiveSize": size,
            "archiveSha256": digest,
            "contentsMatchSourceManifest": True,
            "freshAuthenticatedRestore": True,
        }
        args.receipt.parent.mkdir(parents=True, exist_ok=True)
        args.receipt.write_text(json.dumps(receipt, indent=2) + "\n", encoding="utf-8")
    except (OSError, json.JSONDecodeError, HTTPError, URLError, TimeoutError, ValueError, KeyError, ET.ParseError) as error:
        print(f"NuGet publication receipt blocked: {error}", file=sys.stderr)
        return 1
    print("Verified private package linkage and exact archive bytes through a fresh authenticated NuGet restore.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
