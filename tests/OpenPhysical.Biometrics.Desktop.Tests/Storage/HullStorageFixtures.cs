// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using OpenPhysical.Biometrics.Capture;
using OpenPhysical.Biometrics.Capture.Futronic;
using OpenPhysical.Biometrics.Fingerprints;
using OpenPhysical.Biometrics.Formats.Fingerprints;

internal static class HullStorageFixtures
{
    internal static CapturedFingerprint Capture(bool modified = false) =>
        Capture(ProcessingFrameFixtures.PublicFinger(), modified);

    internal static CapturedFingerprint Capture(byte[] pixels, bool modified = false)
    {
        var capture = StorageFixtures.Capture();
        var frame = ProcessingFrameFixtures.NormalizedFrame(pixels);
        var contact = Fs50ContactAnalysis.Analyze(frame.Pixels.AsSpan()).Value.Diagnostics;
        var contour = Fs50ContactContourRegion
            .Create(
                frame,
                contact,
                Fs50ContactContourPolicy.Create(new Fs50ContactContourOptions()).Value,
                CancellationToken.None
            )
            .Value;
        var region = contour.ToProcessingRegion(modified).Value;
        var artifact = Artifact(frame, region.Mask, modified);
        var original = Assessment(capture, artifact.OriginalDigest, CaptureImageRole.Original, 60);
        var processed = Assessment(
            capture,
            artifact.ProcessedDigest,
            CaptureImageRole.Processed,
            73
        );
        var evidence = Evidence(artifact, original, processed, contact, contour, region);
        return new CaptureAcquisitionPolicy(CapturePolicySettings.Default, 1).ApplyTo(
            capture with
            {
                Frame = frame,
                Nfiq2 = processed.Nfiq2,
                Processing = evidence,
                View = View(capture.View, region.Mask),
            }
        );
    }

    private static FingerprintView View(FingerprintView view, FingerprintProcessingMask mask)
    {
        var index = mask.Inner.IndexOf((byte)1);
        return view with
        {
            Minutiae =
            [
                new FingerprintMinutia(
                    1,
                    (ushort)(index % mask.Width),
                    (ushort)(index / mask.Width),
                    45,
                    90
                ),
            ],
        };
    }

    private static FingerprintPreprocessingArtifact Artifact(
        CaptureFrame frame,
        FingerprintProcessingMask mask,
        bool modified
    ) =>
        FingerprintPreprocessing
            .Process(
                frame,
                mask,
                FingerprintPreprocessingPolicy
                    .Create(claheEnabled: false, contrast: modified ? 30 : 0)
                    .Value,
                4,
                CancellationToken.None
            )
            .Value;

    private static CaptureImageQualityEvidence Assessment(
        CapturedFingerprint capture,
        string digest,
        CaptureImageRole role,
        int score
    ) =>
        new(
            digest,
            role,
            capture.Quality,
            FingerprintUnifiedQualityAssessment.Create(score, "test-model").Value,
            []
        );

    private static CaptureProcessingEvidence Evidence(
        FingerprintPreprocessingArtifact artifact,
        CaptureImageQualityEvidence original,
        CaptureImageQualityEvidence processed,
        Fs50ContactDiagnostics contact,
        Fs50ContactContourArtifact contour,
        Fs50ContourProcessingRegion region
    ) =>
        new(
            artifact,
            original,
            FingerprintQualitySelectionPolicy.Create(2, 7).Value,
            CaptureSelectionReason.Manual,
            [
                new(
                    artifact.OriginalDigest,
                    artifact.ProcessedDigest,
                    artifact.Source.Sequence,
                    artifact.Source.CapturedAt,
                    processed,
                    default,
                    default,
                    contour.Statistics.MeasuredSupportArea,
                    default
                ),
            ],
            contact,
            contour,
            region,
            CaptureProcessingInputRole.DeclaredOfflineImage,
            default
        );
}
