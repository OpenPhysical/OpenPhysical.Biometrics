// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture;
using OpenPhysical.Biometrics.Capture.Futronic;
using OpenPhysical.Biometrics.Fingerprints;
using OpenPhysical.Biometrics.Formats.Fingerprints;

/// <summary>A frozen fingerprint image and its exact canonical export view.</summary>
/// <param name="Frame">Original immutable acquisition.</param>
/// <param name="Finger">Operator-selected finger identity.</param>
/// <param name="View">Exact canonical view shared by export and display.</param>
/// <param name="Quality">Actual assessment of the feature-extraction pixels.</param>
public sealed record CapturedFingerprint(
    CaptureFrame Frame,
    FingerPosition Finger,
    FingerprintView View,
    FingerprintQualityAssessment Quality
)
{
    /// <summary>Gets the retained Segmentation evidence when available.</summary>
    public Maybe<Fs50ContactRegion> Segmentation { get; init; }

    /// <summary>Gets the retained ExtractedCount evidence when available.</summary>
    public Maybe<int> ExtractedCount { get; init; }

    /// <summary>Gets the retained ForegroundCount evidence when available.</summary>
    public Maybe<int> ForegroundCount { get; init; }

    /// <summary>Gets the retained Nfiq2 evidence when available.</summary>
    public Maybe<FingerprintUnifiedQualityAssessment> Nfiq2 { get; init; }

    /// <summary>Gets the retained ExtractorVersion evidence when available.</summary>
    public Maybe<string> ExtractorVersion { get; init; }

    /// <summary>Gets the retained Processing evidence when available.</summary>
    public Maybe<CaptureProcessingEvidence> Processing { get; init; }

    internal FingerprintRecord CanonicalRecord(FingerPosition assignedFinger) =>
        new(
            0,
            0,
            0,
            (ushort)Frame.Width,
            (ushort)Frame.Height,
            197,
            197,
            [View with { FingerPosition = (byte)assignedFinger }]
        );
}
