#!/usr/bin/env python3
# SPDX-License-Identifier: GPL-2.0-or-later
"""Pack modern and optional facial components, then exercise isolated NuGet consumers."""
import argparse
import hashlib
import json
import sys
import os
from pathlib import Path
import subprocess
import tempfile
from xml.sax.saxutils import quoteattr
import zipfile
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
PACKAGES = ROOT / "artifacts/packages"
PROJECTS = ["OpenPhysical.Biometrics.Formats", "OpenPhysical.Biometrics.Piv", "OpenPhysical.Biometrics.Fingerprints", "OpenPhysical.Biometrics.Imaging", "OpenPhysical.Biometrics.Facial", "OpenPhysical.Biometrics", "OpenPhysical.Biometrics.Tool"]
MODEL_PROJECT = {"OpenPhysical.Biometrics.Facial.Models": "OpenPhysical.Biometrics.Onnx"}
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("--skip-pack", action="store_true", help="Consume the most recently built packages.")
parser.add_argument("--require-all-runtimes", action="store_true", help="Validate complete five-runtime dependency and CLI release packages.")
options = parser.parse_args()
subprocess.run([sys.executable, str(ROOT / "scripts/verify-models.py")], check=True, cwd=ROOT)
for name in ([] if options.skip_pack else PROJECTS + list(MODEL_PROJECT.values())):
    arguments = ["dotnet", "pack", str(ROOT / "src" / name), "-c", "Release", "-m:1", "-nr:false", "-p:UseSharedCompilation=false"]
    if name == "OpenPhysical.Biometrics.Tool" and not options.require_all_runtimes:
        # Local verification uses the current host assets; release packing checks all five runtimes.
        arguments.append("-p:AllowPartialBiometricRuntimes=true")
    subprocess.run(arguments, check=True, cwd=ROOT)

versions = {}
for name in PROJECTS + list(MODEL_PROJECT):
    matches = sorted(PACKAGES.glob(name + ".[0-9]*.nupkg"), key=lambda path: path.stat().st_mtime)
    if not matches:
        raise SystemExit(f"Package missing: {name}; run this script with packing enabled.")
    package = matches[-1]
    versions[name] = package.name[len(name) + 1:-6]
    with zipfile.ZipFile(package) as archive:
        names = archive.namelist()
        assert len(names) == len(set(names)), f"Duplicate package entries: {package}"
        assert not any("fixtures/" in path or path.endswith((".gray8", ".png")) and path != "icon.png" for path in names), package
        metadata = archive.read(name + ".nuspec").decode()
        assert "SixLabors" not in metadata and "ImageSharp" not in metadata, package
        assert "THIRD_PARTY_NOTICES.md" in names, package
        assert any(path.endswith("LICENSE-MediaPipe-Apache-2.0.txt") for path in names), package
        assert "LICENSE" in names, package
        assert not any("FaceDetector.onnx" in path or "landmarks_68_pfld" in path or "FaceONNX" in path for path in names), package
        assert not any(path.lower().endswith((".p12", ".pfx", ".key")) for path in names), package
        if name == "OpenPhysical.Biometrics.Facial.Models":
            provenance = json.loads(archive.read("models/provenance.json"))
            assert provenance == json.loads((ROOT / "src/OpenPhysical.Biometrics.Onnx/Resources/provenance.json").read_text()), package
            assert "models/LICENSE-MediaPipe-Apache-2.0.txt" in names, package
            assert "models/README.md" in names, package

# Algorithm versions come from the same properties used by the product projects.
properties = ET.parse(ROOT / "Directory.Build.props").getroot()
nfiq_version = properties.findtext(".//NfiqPackageVersion")
if options.require_all_runtimes:
    expected_version = properties.findtext(".//Version")
    assert all(value == expected_version for value in versions.values()), "Release packages must share the current product version."
    dependencies = [
        ("OpenPhysical.Biometrics.NBIS", "NbisPackageVersion", "openphysical_nbis"),
        ("OpenPhysical.Biometrics.NFIQ1", "NfiqPackageVersion", "openphysical_nfiq1"),
        ("OpenPhysical.Biometrics.NFIQ2", "NfiqPackageVersion", "openphysical_nfiq2"),
    ]
    for package_id, version_property, native_name in dependencies:
        repository = "OpenPhysical.Biometrics.NBIS" if package_id.endswith("NBIS") else "OpenPhysical.Biometrics.NFIQ"
        dependency = ROOT.parent / repository / "artifacts/packages" / f"{package_id}.{properties.findtext('.//' + version_property)}.nupkg"
        with zipfile.ZipFile(dependency) as archive:
            for rid in ("win-x64", "linux-x64", "linux-arm64", "osx-x64", "osx-arm64"):
                extension = "dll" if rid.startswith("win") else "so" if rid.startswith("linux") else "dylib"
                prefix = "" if rid.startswith("win") else "lib"
                expected = f"runtimes/{rid}/native/{prefix}{native_name}.{extension}"
                assert expected in archive.namelist(), f"Missing release runtime: {dependency}: {expected}"
    # The CLI embeds its complete runtime dependencies in the tool package.
    tool = PACKAGES / f"OpenPhysical.Biometrics.Tool.{versions['OpenPhysical.Biometrics.Tool']}.nupkg"
    with zipfile.ZipFile(tool) as archive:
        for rid in ("win-x64", "linux-x64", "linux-arm64", "osx-x64", "osx-arm64"):
            for native_name in ("openphysical_nbis", "openphysical_nfiq1"):
                assert any(f"/runtimes/{rid}/native/" in name and native_name in name for name in archive.namelist()), f"CLI missing {rid}/{native_name}"

with tempfile.TemporaryDirectory(prefix="biometrics-consumer-") as directory:
    work = Path(directory)
    sources = [PACKAGES, ROOT.parent / "OpenPhysical.Biometrics.NBIS/artifacts/packages", ROOT.parent / "OpenPhysical.Biometrics.NFIQ/artifacts/packages", "https://api.nuget.org/v3/index.json"]
    config = work / "NuGet.Config"
    config.write_text('<configuration><packageSources><clear/>' + ''.join('<add key="feed' + str(i) + '" value=' + quoteattr(str(path)) + '/>' for i, path in enumerate(sources)) + '</packageSources></configuration>')
    references = ["OpenPhysical.Biometrics", "OpenPhysical.Biometrics.Facial", "OpenPhysical.Biometrics.Imaging"]
    (work / "Consumer.csproj").write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><TreatWarningsAsErrors>true</TreatWarningsAsErrors></PropertyGroup><ItemGroup>' + ''.join('<PackageReference Include="' + name + '" Version="' + versions[name] + '"/>' for name in references) + '<PackageReference Include="OpenPhysical.Biometrics.NFIQ2" Version="' + nfiq_version + '"/></ItemGroup></Project>')
    (work / "Program.cs").write_text(r'''using OpenPhysical.Biometrics.Formats.Fingerprints;
using OpenPhysical.Biometrics.Facial;
using OpenPhysical.Biometrics.Imaging;
using OpenPhysical.Biometrics.NBIS;
using OpenPhysical.Biometrics.NFIQ1;
using OpenPhysical.Biometrics.NFIQ2;
using OpenPhysical.Biometrics.Piv;
using System.Collections.Immutable;

// Exercise native fingerprint processing before touching facial services.
var pixels = File.ReadAllBytes(args[0]);
var capture = new FingerprintImage(pixels, 512, 512, 500);
var engine = new NbisFingerprintEngine();
var template = engine.Extract(capture);
if (engine.Match(template, template) <= 0) throw new InvalidOperationException("Native matching failed.");
var quality = new Nfiq1Engine().Assess(capture.Pixels, 512, 512, 500);
if (quality.QualityClass is < 1 or > 5) throw new InvalidOperationException("Quality assessment failed.");
using var nfiq2 = new Nfiq2Engine();
if (nfiq2.Assess(capture.Pixels, 512, 512, 500).UnifiedQualityScore is < 0 or > 100)
    throw new InvalidOperationException("NFIQ 2 quality assessment failed.");
if (AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name!.Contains("Onnx"))) throw new InvalidOperationException("Fingerprint processing loaded ONNX.");
var view = new FingerprintView(2, 0, 0, 80, [new OpenPhysical.Biometrics.Formats.Fingerprints.FingerprintMinutia(1, 10, 20, 30, 90)], []);
var record = new FingerprintRecord(1, 1, 0x8001, 512, 512, 197, 197, [view]);
var bytes = Incits378Codec.Write(record);
if (!Incits378Codec.Parse(bytes).IsSuccess) throw new InvalidOperationException("Format round trip failed.");
if (!BiometricInspector.Inspect(bytes, DateTimeOffset.UtcNow).Structure.Diagnostics.IsEmpty) throw new InvalidOperationException("Inspection failed.");
using var image = RgbImage.FromRgbBytes(new byte[32 * 32 * 3], 32, 32);
using var png = new MemoryStream();
image.SaveAsPng(png);
using var restored = RgbImage.Load(png.ToArray());
if (restored.Width != 32 || restored.Height != 32) throw new InvalidOperationException("Image round trip failed.");
// Use deterministic samples to exercise the actual packaged JPEG 2000 encoder.
var portrait = new byte[480 * 640 * 3];
new Random(42).NextBytes(portrait);
var prepared = new FacialRecordPreparer().Prepare(portrait, 480, 640,
    new FacialAcquisitionMetadata(DateTimeOffset.UtcNow, 0, 2, true, true, 80));
if (prepared.Status != FacialPreparationStatus.Prepared || prepared.Record is null)
    throw new InvalidOperationException("Facial record preparation failed.");
if (!PivBiometricProfile.Validate(prepared.Record).IsEmpty)
    throw new InvalidOperationException("Prepared facial metadata failed its profile.");
File.WriteAllBytes(args[1], bytes);
Console.WriteLine("Isolated package workflows passed.");
''')
    environment = dict(os.environ, NUGET_PACKAGES=str(work / "packages"))
    def run(*arguments, expected=0):
        result = subprocess.run(arguments, cwd=work, env=environment)
        if result.returncode != expected:
            raise RuntimeError(f"Command returned {result.returncode}, expected {expected}: {arguments[0]}")
    fixture = ROOT.parent / "OpenPhysical.Biometrics.NBIS/tests/fixtures/sd04/f0005_03.gray8"
    output = work / "record.fmr"
    run("dotnet", "restore", "Consumer.csproj", "--configfile", str(config))
    modern_assets = json.loads((work / "obj/project.assets.json").read_text())["libraries"]
    assert not any("SixLabors" in name or "ImageSharp" in name for name in modern_assets), "Modern dependency graph must use the Skia image backend."
    run("dotnet", "run", "--project", "Consumer.csproj", "-c", "Release", "--no-restore", "--", str(fixture), str(output))
    run("dotnet", "tool", "install", "OpenPhysical.Biometrics.Tool", "--version", versions["OpenPhysical.Biometrics.Tool"], "--tool-path", str(work / "tools"), "--configfile", str(config))
    command = work / "tools" / ("biotool.exe" if os.name == "nt" else "biotool")
    run(str(command), "inspect", str(output), "--json")
    run(sys.executable, str(ROOT / "scripts/test-portrait-cli.py"), str(command))
    converted = work / "template.json"
    run(str(command), "convert", str(output), str(converted), "--finger", "2", "--view", "0")
    serialized = converted.read_bytes()
    assert json.loads(serialized), "Converted template must be valid JSON."
    run(str(command), "convert", str(output), str(converted), "--finger", "2", "--view", "0", expected=1)
    assert converted.read_bytes() == serialized, "Conversion must preserve an existing output."
    malformed = work / "malformed.fmr"
    malformed.write_bytes(b"FMR")
    rejected = work / "rejected.json"
    run(str(command), "convert", str(malformed), str(rejected), "--finger", "2", "--view", "0", expected=1)
    assert not rejected.exists(), "Malformed records must preserve the output directory."
    # Exercise the optional facial dependency graph in a separate process. The
    # fingerprint consumer above already proved that its operations leave ONNX idle.
    legacy = work / "facial"
    legacy.mkdir()
    (legacy / "Facial.csproj").write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><TreatWarningsAsErrors>true</TreatWarningsAsErrors></PropertyGroup><ItemGroup><PackageReference Include="OpenPhysical.Biometrics.Facial" Version="' + versions["OpenPhysical.Biometrics.Facial"] + '"/></ItemGroup></Project>')
    (legacy / "Program.cs").write_text(r'''using OpenPhysical.Biometrics.Facial;
var resources = typeof(OpenPhysical.Biometrics.Onnx.MediaPipeModels).Assembly.GetManifestResourceNames();
if (resources.Length != 2 || !resources.Contains(OpenPhysical.Biometrics.Onnx.MediaPipeModels.Detector) || !resources.Contains(OpenPhysical.Biometrics.Onnx.MediaPipeModels.Landmarks))
    throw new InvalidOperationException("The model package must contain exactly the selected MediaPipe assets.");
using var service = new PortraitPreparer();
var result = service.Prepare(File.ReadAllBytes(args[0]));
if (result.Faces.Length != 1 || result.Faces[0].Landmarks.Length != 478)
    throw new InvalidOperationException("Packaged MediaPipe detection failed.");
Console.WriteLine("Isolated MediaPipe facial package workflow passed.");
''')
    (legacy / "model-provenance.json").write_text(json.dumps(provenance))
    run("dotnet", "restore", str(legacy / "Facial.csproj"), "--configfile", str(config))
    legacy_assets = json.loads((legacy / "obj/project.assets.json").read_text())["libraries"]
    assert not any("SixLabors" in name or "ImageSharp" in name or "CSharpFunctionalExtensions" in name or "FaceONNX" in name for name in legacy_assets), "Optional facial dependencies must use the shared Skia backend."
    run("dotnet", "run", "--project", str(legacy / "Facial.csproj"), "-c", "Release", "--no-restore", "--", str(ROOT / "tests/OpenPhysical.Biometrics.Facial.Tests/Fixtures/glenn.jpg"), str(legacy / "model-provenance.json"))
print("Isolated library and CLI package checks passed.")
