// SPDX-License-Identifier: GPL-2.0-or-later
using OpenPhysical.Biometrics.Capture;
using OpenPhysical.Biometrics.Capture.Futronic;

namespace OpenPhysical.Biometrics.Desktop.Tests;

internal static class CaptureViewportRevision
{
    internal static CapturedFingerprint MoreContext(CapturedFingerprint source)
    {
        var evidence = source.Processing.Value;
        var policy = Fs50ContactContourPolicy
            .Create(evidence.Contour.Policy.Options with { ContextMargin = 96 })
            .Value;
        var contour = evidence.Contour.WithContext(policy, CancellationToken.None).Value;
        var region = contour.ToProcessingRegion(false).Value;
        var artifact = FingerprintPreprocessing
            .Process(
                source.Frame,
                region.Mask,
                evidence.Artifact.Policy,
                evidence.Artifact.ProcessingRevision,
                CancellationToken.None
            )
            .Value;
        return source with
        {
            Processing = evidence with
            {
                Artifact = artifact,
                Contour = contour,
                ProcessingRegion = region,
            },
        };
    }
}
