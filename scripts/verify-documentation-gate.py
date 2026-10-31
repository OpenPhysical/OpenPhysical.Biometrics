#!/usr/bin/env python3
# SPDX-License-Identifier: GPL-2.0-or-later
# Copyright (c) 2026 Mistial Developer

"""Prove normal builds accept documented APIs and reject missing docs or disabled gates."""
import subprocess
import tempfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
artifacts = ROOT / "artifacts"
artifacts.mkdir(exist_ok=True)
valid = '''namespace DocumentationGateProbe;
/// <summary>Exercises the repository public API documentation policy.</summary>
public static class Probe
{
    /// <summary>Returns the supplied value.</summary>
    /// <param name="value">Value returned unchanged.</param>
    /// <returns>The supplied integer.</returns>
    public static int Echo(int value) => value;
}
'''
with tempfile.TemporaryDirectory(prefix="documentation-gate-", dir=artifacts) as directory:
    work = Path(directory)
    project = work / "DocumentationGateProbe.csproj"
    project.write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework><IsPackable>false</IsPackable></PropertyGroup></Project>')
    source = work / "Probe.cs"

    def build(text, expected=None, properties=()):
        source.write_text(text)
        command = ["dotnet", "build", str(project), "-c", "Release", "--verbosity", "quiet", "-m:1", "-nr:false", "-p:UseSharedCompilation=false", "-p:IsConsolidatedBiometricsProject=true", *properties]
        result = subprocess.run(command, capture_output=True, text=True)
        output = result.stdout + result.stderr
        if expected is None:
            if result.returncode != 0:
                raise SystemExit(output)
        elif result.returncode == 0 or expected not in output:
            raise SystemExit(f"Expected failure containing {expected}:\n{output}")
        print(f"Verified documentation gate: {expected or 'documented API accepted'}")

    build(valid)
    build(valid.replace('    /// <summary>Returns the supplied value.</summary>\n    /// <param name="value">Value returned unchanged.</param>\n    /// <returns>The supplied integer.</returns>\n', ''), "CS1591")
    build(valid.replace('    /// <param name="value">Value returned unchanged.</param>\n', ''), "SA1611")
    build(valid.replace('    /// <returns>The supplied integer.</returns>\n', ''), "SA1615")
    build(valid.replace('Returns the supplied value.', 'Uses <see cref="TypeThatDoesNotExist"/>.'), "CS1574")
    build(valid, "cannot be suppressed", ("-p:NoWarn=CS1591",))
    build(valid, "Warnings must be treated as errors", ("-p:TreatWarningsAsErrors=false",))
    build(valid, "Documentation analyzers must run", ("-p:RunAnalyzers=false",))
print("All public API documentation enforcement checks passed.")
