#!/usr/bin/env python3
# SPDX-License-Identifier: GPL-2.0-or-later
"""Rebuild the pinned historical first-party analyzer and verify public baseline masks.

Run after building Desktop, with the verified external Gray8 directory as argument.
This is a validation oracle, not a production fallback or alternate NIST engine.
"""

import argparse
import gzip
import hashlib
import html
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import zipfile
from baseline_preflight import preflight


PROGRAM = r'''
using OpenPhysical.Biometrics.Capture.Futronic;
using System.IO.Compression;
using System.Text.Json;
var rawFolder=args[0]; var output=Path.Combine(rawFolder,"legacy");
Directory.CreateDirectory(output); var rows=new List<object>();
var console=Console.Out; Console.SetOut(TextWriter.Null);
using var manifest=JsonDocument.Parse(File.ReadAllBytes(args[1]));
foreach(var item in manifest.RootElement.GetProperty("images").EnumerateArray()) {
    var id=item.GetProperty("id").GetString()!;
    var width=item.GetProperty("width").GetInt32();var height=item.GetProperty("height").GetInt32();
    var raw=File.ReadAllBytes(Path.Combine(rawFolder,id+".gray8"));
    if(width<1||width>800||height<1||height>750||raw.Length!=width*height) {
        Console.Error.WriteLine("Invalid declared public geometry.");return 1;
    }
    var pixels=Enumerable.Repeat((byte)255,800*750).ToArray();
    for(int y=0;y<height;y++) raw.AsSpan(y*width,width).CopyTo(pixels.AsSpan((y+(750-height)/2)*800+(800-width)/2,width));
    var result=Fs50ContactAnalysis.Analyze(pixels);
    if(result.IsFailure) { Console.Error.WriteLine(result.Error.Code); return 1; }
    var region=result.Value;
    using(var file=File.Create(Path.Combine(output,id+".mask.gz")))
    using(var gzip=new GZipStream(file,CompressionLevel.SmallestSize)) gzip.Write(region.Mask.AsSpan());
    rows.Add(new{id,present=region.Present,count=region.ComponentCount,issue=region.Issue.ToString()});
}
File.WriteAllText(Path.Combine(output,"status.json"),JsonSerializer.Serialize(rows));
Console.SetOut(console); Console.WriteLine("Generated historical baseline masks."); return 0;
'''


def prepare(raw_folder: Path, manifest_path: Path, record: bool, policy_freeze=None) -> int:
    repository = Path(__file__).resolve().parents[1]
    manifest, snapshot, error = preflight(repository, raw_folder, manifest_path, policy_freeze)
    if error:
        print(error, file=sys.stderr)
        return 1
    role = manifest.get("role", "diagnostic")
    if record and role not in ("development", "final"):
        print("Baseline bootstrap requires a development cohort or an explicitly frozen final policy.", file=sys.stderr)
        return 1
    parent = repository / "artifacts/validation"
    parent.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix="historical-baseline-", dir=parent) as generated:
        result = generate(repository, raw_folder, manifest_path, manifest, snapshot,
                          Path(generated), policy_freeze)
    if result:
        return result
    return verify_masks(raw_folder, manifest_path, manifest, record)


def generate(repository, raw_folder, manifest_path, manifest, snapshot, target, policy_freeze):
    with zipfile.ZipFile(snapshot) as source:
        expected = manifest["legacyAnalyzerSourceFiles"]
        if set(source.namelist()) != set(expected):
            print("Historical source member mismatch.", file=sys.stderr)
            return 1
        for name, digest in expected.items():
            data = source.read(name)
            if hashlib.sha256(data).hexdigest() != digest:
                print("Historical source hash mismatch: " + name, file=sys.stderr)
                return 1
            (target / name).write_bytes(data)
    reference_root = repository / "artifacts/bin/OpenPhysical.Biometrics.Desktop/Release/net10.0"
    if manifest.get("role") == "final":
        reference_root = Path(json.loads(policy_freeze.read_text())["ExecutionRoot"])
    binaries = html.escape(str(reference_root / "*.dll"))
    (target / "Program.cs").write_text(PROGRAM)
    (target / "baseline.csproj").write_text(
        '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType>'
        '<TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings>'
        '<NoWarn>CS0436</NoWarn></PropertyGroup><ItemGroup>'
        f'<Reference Include="{binaries}" /></ItemGroup></Project>'
    )
    build = subprocess.run(["dotnet", "build", str(target / "baseline.csproj"), "-c", "Release",
                            "-m:1", "-nr:false", "--disable-build-servers",
                            "-p:ImportDirectoryBuildProps=false", "-p:ImportDirectoryBuildTargets=false"], check=False)
    if build.returncode:
        return build.returncode
    run = subprocess.run(["dotnet", str(target / "bin/Release/net10.0/baseline.dll"), str(raw_folder), str(manifest_path)], check=False)
    if run.returncode:
        return run.returncode
    return 0


def verify_masks(raw_folder, manifest_path, manifest, record):
    statuses = {row["id"]: row for row in json.loads((raw_folder / "legacy/status.json").read_text())}
    for item in manifest["images"]:
        pixels = gzip.decompress((raw_folder / "legacy" / (item["id"] + ".mask.gz")).read_bytes())
        status = statuses[item["id"]]
        actual = dict(legacyMaskSha256=hashlib.sha256(pixels).hexdigest(), legacyPresent=status["present"],
                      legacyComponentCount=status["count"], legacyIssue=status["issue"])
        raw = (raw_folder / (item["id"] + ".gray8")).read_bytes()
        padded = bytearray([255]) * (800 * 750)
        width, height = item["width"], item["height"]
        for y in range(height):
            start = (y + (750-height)//2) * 800 + (800-width)//2
            padded[start:start+width] = raw[y*width:(y+1)*width]
        actual["paddedOriginalSha256"] = hashlib.sha256(padded).hexdigest()
        if record and "legacyMaskSha256" not in item:
            item.update(actual)
        elif any(item.get(key) != value for key, value in actual.items()):
            print("Historical baseline differs: " + item["id"], file=sys.stderr)
            return 1
    if record:
        manifest_path.write_text(json.dumps(manifest, indent=2) + "\n")
    print(f"Verified all {len(manifest['images'])} historical masks and contact decisions.")
    return 0


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("raw_folder", type=Path)
    parser.add_argument("--manifest", type=Path, default=Path(__file__).resolve().parents[1] /
                        "tests/OpenPhysical.Biometrics.Desktop.Tests/Fixtures/fvc2002/manifest.json")
    parser.add_argument("--record-baseline", action="store_true")
    parser.add_argument("--policy-freeze", type=Path,
                        help="Verified preflight receipt required before final-cohort baseline analysis.")
    args = parser.parse_args()
    sys.exit(prepare(args.raw_folder.resolve(), args.manifest.resolve(), args.record_baseline,
                     args.policy_freeze.resolve() if args.policy_freeze else None))
