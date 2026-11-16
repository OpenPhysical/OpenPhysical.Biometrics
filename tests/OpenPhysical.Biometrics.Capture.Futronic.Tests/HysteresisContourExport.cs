// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic.Tests;

internal static class HysteresisContourExport
{
    internal static bool Write(CaptureFrame frame, Fs50ContactContourArtifact contour)
    {
        var folder = Environment.GetEnvironmentVariable("OPENPHYSICAL_CONTACT_CONTOUR_IMAGES");
        if (string.IsNullOrWhiteSpace(folder))
            return false;
        var options = Fs50EvidencePolicy.Default.Options;
        var primaryOnly = Fs50EvidencePolicy
            .Create(
                options with
                {
                    FaintVariance = options.TextureVariance,
                    FaintMean = options.TextureMean,
                }
            )
            .Value;
        var primary = Fs50ContactAnalysis.Analyze(frame, primaryOnly).Value;
        var control = Fs50ContactContourRegion
            .Create(
                frame,
                primary.Diagnostics,
                Fs50ContactContourPolicy.Default,
                CancellationToken.None
            )
            .Value;
        _ = Directory.CreateDirectory(folder);
        File.WriteAllBytes(Path.Combine(folder, "synthetic-annulus.gray8"), frame.Pixels.ToArray());
        File.WriteAllBytes(
            Path.Combine(folder, "primary-only-control.mask"),
            control.FeatureMask.ToArray()
        );
        File.WriteAllBytes(
            Path.Combine(folder, "supported-hysteresis.mask"),
            contour.FeatureMask.ToArray()
        );
        return true;
    }
}
