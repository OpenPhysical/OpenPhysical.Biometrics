// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Fingerprints;

using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture;

/// <summary>Unsigned local lineage for one view in an exact prepared INCITS record.</summary>
/// <remarks>This is processing evidence, not operator authorization, sensor authentication or a CA signature.</remarks>
public sealed class FingerprintPreparationEvidence
{
    internal FingerprintPreparationEvidence(
        PreparedAcquiredFingerprint prepared,
        string recordSha256,
        Maybe<string> pixelSha256
    )
    {
        Finger = prepared.Selection.Finger;
        Availability = prepared.Selection.Availability;
        AcquisitionProfile = prepared.Profile;
        Provenance = prepared.Frame.Map(frame => frame.Provenance);
        CapturedAt = prepared.Frame.Map(frame => frame.CapturedAt);
        FrameSequence = prepared.Frame.Map(frame => frame.Sequence);
        CorrectedPixelSha256 = pixelSha256;
        Quality = prepared.Selection.Quality;
        ExtractorProvenance = prepared.Selection.Template.Map(template => template.Provenance);
        RecordSha256 = recordSha256;
    }

    /// <summary>Gets the selected finger represented by this view.</summary>
    public FingerPosition Finger { get; }

    /// <summary>Gets the explicit availability state, independent of capture quality.</summary>
    public FingerprintAvailability Availability { get; }

    /// <summary>Gets the approved acquisition mapping when a capture exists.</summary>
    public Maybe<FingerprintAcquisitionProfile> AcquisitionProfile { get; }

    /// <summary>Gets calibration, geometry, resolution and driver evidence without image pixels.</summary>
    public Maybe<CaptureProvenance> Provenance { get; }

    /// <summary>Gets the acquisition instant when a capture exists.</summary>
    public Maybe<DateTimeOffset> CapturedAt { get; }

    /// <summary>Gets the acquisition session frame sequence when a capture exists.</summary>
    public Maybe<long> FrameSequence { get; }

    /// <summary>Gets the SHA-256 of the exact corrected Gray8 pixels used for extraction.</summary>
    public Maybe<string> CorrectedPixelSha256 { get; }

    /// <summary>Gets the assessment and its actual quality algorithm identity.</summary>
    public Maybe<FingerprintQualityAssessment> Quality { get; }

    /// <summary>Gets the extraction provider's description from the prepared template.</summary>
    public Maybe<string> ExtractorProvenance { get; }

    /// <summary>Gets the versioned selection and INCITS conversion algorithm.</summary>
    public string ProcessingVersion => "OpenPhysical.PivEnrollmentRecordBuilder/v1";

    /// <summary>Gets the SHA-256 of the complete encoded unsigned INCITS record.</summary>
    public string RecordSha256 { get; }
}
