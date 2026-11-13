// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture.Camera;
using OpenPhysical.Biometrics.Facial;
using OpenPhysical.Biometrics.Piv;

/// <summary>One explicitly accepted facial object and the evidence for its exact cropped pixels.</summary>
/// <param name="ObjectBytes">Canonical INCITS 385 record bytes encoded once at acceptance.</param>
/// <param name="Portrait">Prepared cropped pixels and geometry findings.</param>
/// <param name="Acquisition">Camera and attended acquisition findings.</param>
/// <param name="Source">Actual camera identity.</param>
/// <param name="Mode">Actual native capture mode.</param>
public sealed record FaceAcceptedCapture(
    ImmutableArray<byte> ObjectBytes,
    PortraitPreparationResult Portrait,
    FacialAcquisitionMetadata Acquisition,
    CameraSource Source,
    CameraMode Mode
)
{
    /// <summary>Gets advisory measurements of these exact accepted portrait pixels.</summary>
    public Maybe<PortraitQualityMeasurements> Quality { get; init; }

    /// <summary>Gets unknown acquisition evidence separately from the unsigned object's metadata findings.</summary>
    public ImmutableArray<BiometricProfileDiagnostic> AcquisitionFindings { get; init; } = [];

    /// <summary>Gets the actual optical measurement snapshotted at acceptance, independent of later setup changes.</summary>
    public Maybe<CameraOpticalQualification> OpticalQualification { get; init; }

    /// <summary>Gets the source-bound automatic failures explicitly overridden for unsigned staging.</summary>
    public Maybe<FacialCaptureOverride> CaptureOverride { get; init; }

    /// <summary>Gets the operator's portrait review, separately from automatic checks and overrides.</summary>
    public bool PortraitReviewed { get; init; }
}
