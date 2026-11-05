// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Fingerprints;

using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture;

/// <summary>Maps qualified acquisition evidence to the existing plain fingerprint processing input.</summary>
public sealed class AcquiredFingerprintAdapter
{
    /// <summary>Preserves corrected pixels and checks the approved acquisition mapping.</summary>
    /// <param name="frame">Corrected acquisition frame.</param>
    /// <param name="finger">Finger selected by the guided workflow.</param>
    /// <param name="profile">Acquisition policy already approved by the calling authority.</param>
    /// <returns>A plain live capture or a precise evidence failure.</returns>
    public Result<FingerprintCapture, FingerprintError> Adapt(
        CaptureFrame frame,
        FingerPosition finger,
        FingerprintAcquisitionProfile profile
    )
    {
        if (frame is null || profile is null)
            return Invalid("A frame and approved acquisition profile are required.");
        var provenance = frame.Provenance;
        if (
            frame.Stage != CaptureFrame.FrameStage.Corrected
            || provenance.Settings.Rolled
            || provenance.OutputGeometry != profile.OutputGeometry
            || !provenance.Qualification.IsQualified
            || provenance.Qualification.ProfileId != profile.QualificationId
            || provenance.Device.DeviceId != profile.DeviceId
            || provenance.DriverVersion != profile.DriverVersion
            || provenance.CorrectionVersion != profile.CorrectionVersion
            || !provenance
                .CalibrationDigest.AsSpan()
                .SequenceEqual(profile.CalibrationDigest.AsSpan())
        )
            return Invalid("The frame does not match the approved plain acquisition profile.");
        if (!HasProcessingResolution(provenance.Resolution))
            return new FingerprintError(
                FingerprintErrorCodes.UnsupportedResolution,
                "This processing path requires measured corrected geometry at 500 pixels per inch on both axes."
            );
        return FingerprintCapture.Create(
            frame.Pixels.AsSpan(),
            frame.Width,
            frame.Height,
            500,
            finger,
            frame.CapturedAt,
            FingerprintImpression.PlainLiveScan,
            profile.EquipmentId,
            profile.AppendixFCertified
        );
    }

    private static bool HasProcessingResolution(CaptureResolution resolution) =>
        resolution.Kind == CaptureResolution.EvidenceKind.Measured
        && resolution.HorizontalPpi.HasValue
        && resolution.VerticalPpi.HasValue
        && resolution.HorizontalPpi.Value == 500
        && resolution.VerticalPpi.Value == 500;

    private static Result<FingerprintCapture, FingerprintError> Invalid(string message) =>
        new FingerprintError(FingerprintErrorCodes.AcquisitionProfileMismatch, message);
}
