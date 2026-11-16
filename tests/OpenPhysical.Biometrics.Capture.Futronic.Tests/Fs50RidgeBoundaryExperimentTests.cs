// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic.Tests;

using System.Text.Json;
using NUnit.Framework;

[TestFixture]
internal sealed class Fs50RidgeBoundaryExperimentTests
{
    [TestCase(16)]
    [TestCase(32)]
    [TestCase(48)]
    [Explicit("Fixed development observations, not an accuracy or publication gate.")]
    public void ExportFixedStructuralToleranceObservations(int tolerance)
    {
        var rows = RidgeBoundaryControls
            .All()
            .Select(control => Observe(control, tolerance))
            .ToArray();
        var folder = Environment.GetEnvironmentVariable("OPENPHYSICAL_RIDGE_BOUNDARY_EXPERIMENT");
        if (string.IsNullOrWhiteSpace(folder))
            folder = Path.Combine(Path.GetTempPath(), "openphysical-ridge-boundary-experiment");
        _ = Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "tolerance-" + tolerance + ".json");
        File.WriteAllText(
            path,
            JsonSerializer.Serialize(
                new
                {
                    Protocol = "fixed-ridge-rejection-envelope-development-v1",
                    RidgeBoundaryTolerance = tolerance,
                    SupportedExpansion = 16,
                    MinimumUncorroboratedArea = 4000,
                    NativeCalls = 0,
                    NamedControlsPassed = rows.Where(row => row.Requirement != "diagnostic")
                        .All(row => row.Matches),
                    Rows = rows,
                },
                new JsonSerializerOptions { WriteIndented = true }
            )
        );
        TestContext.WriteLine(path);
    }

    private static RidgeBoundaryObservation Observe(RidgeBoundaryControl control, int tolerance)
    {
        var options = Fs50EvidencePolicy.Default.Options with
        {
            RidgeBoundaryTolerance = tolerance,
        };
        if (control.PrimaryOnly)
            options = options with
            {
                FaintVariance = options.TextureVariance,
                FaintMean = options.TextureMean,
            };
        var evidence = Fs50EvidencePolicy.Create(options).Value;
        var defaults = Fs50WholeContactPolicy.Default;
        var policy = Fs50WholeContactPolicy
            .Create(defaults.Options, evidence, defaults.EmptySensor, defaults.Presentation)
            .Value;
        using var rig = new WholeContactRig(policy);
        var learned = rig.Learn(control.Reference);
        var observed = rig.Observe(control.Pixels);
        return RidgeBoundaryObservation.From(control, learned, observed, evidence.Digest);
    }
}
