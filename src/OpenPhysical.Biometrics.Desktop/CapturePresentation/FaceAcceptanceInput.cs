// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture.Camera;
using OpenPhysical.Biometrics.Facial;

internal sealed record FaceAcceptanceInput(
    PortraitPreparationResult Portrait,
    FacialAcquisitionMetadata Acquisition,
    CameraSource Source,
    CameraMode Mode,
    Maybe<PortraitQualityMeasurements> Quality,
    FacialPreparationPolicy Policy
)
{
    internal Maybe<CameraOpticalQualification> OpticalQualification { get; init; }
    internal bool PortraitReviewed { get; init; }
}
