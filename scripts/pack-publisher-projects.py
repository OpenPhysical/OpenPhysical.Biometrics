#!/usr/bin/env python3
"""Pack only the repository-owned shared packages listed in the publisher catalog."""

import argparse
import json
from pathlib import Path
import re
import subprocess
import sys

import camera_native_closure


ROOT = Path(__file__).resolve().parents[1]
COMMIT_RE = re.compile(r"^[0-9a-f]{40}$")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--catalog", type=Path, default=Path(__file__).with_name("nuget-package-projects.json"))
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--source-commit", required=True)
    parser.add_argument("--package-version", help="override the package version for a consumer-pinned bootstrap package")
    parser.add_argument("--camera-native-root", type=Path,
                        help="verified osx-arm64 and osx-x64 camera artifacts for the camera NuGet package")
    args = parser.parse_args()
    try:
        if not COMMIT_RE.fullmatch(args.source_commit.lower()):
            raise ValueError("source commit must be a full 40-character Git SHA")
        catalog = json.loads(args.catalog.read_text(encoding="utf-8"))
        projects = catalog.get("projects")
        if not isinstance(projects, list) or not projects:
            raise ValueError("publisher catalog must list package projects")
        project_paths = []
        for relative in projects:
            path = (ROOT / relative).resolve()
            if not path.is_relative_to(ROOT) or not path.is_file() or path.suffix != ".csproj":
                raise ValueError(f"invalid package project path: {relative}")
            project_paths.append(path)
        camera_projects = [project for project in project_paths if project.stem == "OpenPhysical.Biometrics.Capture.Camera"]
        if camera_projects and args.camera_native_root is None:
            raise ValueError("the publisher catalog includes Camera; --camera-native-root is required")
        if args.camera_native_root is not None:
            camera_native_closure.verify_closure(args.camera_native_root.resolve(), args.source_commit.lower())
        args.output.mkdir(parents=True, exist_ok=True)
        for project in project_paths:
            command = [
                "dotnet", "pack", str(project), "-c", "Release", "--no-build", "--no-restore",
                "--output", str(args.output.resolve()), "-p:RepositoryCommit=" + args.source_commit.lower(),
                "-p:PublishRepositoryUrl=true", "-p:ContinuousIntegrationBuild=true",
                *(["-p:Version=" + args.package_version, "-p:PackageVersion=" + args.package_version]
                  if args.package_version else []),
            ]
            if project in camera_projects:
                command.extend(["-p:IncludeCameraNativeClosure=true",
                                "-p:CameraNativePackageRoot=" + str(args.camera_native_root.resolve())])
            subprocess.run(command, cwd=ROOT, check=True)
    except (OSError, json.JSONDecodeError, ValueError, subprocess.CalledProcessError) as error:
        print(f"Package packing blocked: {error}", file=sys.stderr)
        return 1
    print(f"Packed {len(project_paths)} catalogued package projects from the tested checkout.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
