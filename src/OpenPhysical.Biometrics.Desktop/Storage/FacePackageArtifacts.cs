// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.Collections.Immutable;
using System.Text.Json;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Facial;
using OpenPhysical.Biometrics.Packages;

internal static class FacePackageArtifacts
{
    internal static Result<ImmutableArray<PackageArtifact>, DesktopError> Create(
        FaceAcceptedCapture value
    ) =>
        PackageArtifact
            .Create("face.bin", value.ObjectBytes.AsSpan(), "face", "ANSI/INCITS 385-2004")
            .MapError(Error)
            .Bind(record =>
                PackageArtifact
                    .Create("face.json", Evidence(value), "face-evidence", "application/json")
                    .MapError(Error)
                    .Map(evidence => ImmutableArray.Create(record, evidence))
            );

    private static byte[] Evidence(FaceAcceptedCapture value) =>
        JsonSerializer.SerializeToUtf8Bytes(
            new
            {
                sourceSha256 = value.Portrait.SourceSha256,
                width = PortraitOutput.Width,
                height = PortraitOutput.Height,
                sourceType = value.Acquisition.SourceType,
                capturedAt = value.Acquisition.CapturedAt,
                camera = value.Source.Name,
                mode = value.Mode,
                value.Portrait.ModelRevision,
                value.Portrait.GeometryPolicy,
                sourcePose = value
                    .Portrait.Faces.SelectMany(face => FaceQualityEvidence.Values(face.Pose))
                    .ToArray(),
                head = FaceQualityEvidence.Values(value.Portrait.Measurements),
                advisoryQuality = value.Quality.Match(
                    FaceQualityEvidence.Create,
                    () => JsonSerializer.SerializeToElement(new { Available = false })
                ),
                fullFrontalReviewed = value.Acquisition.FullFrontalConfirmed,
                portraitReviewed = value.PortraitReviewed,
                failedCaptureChecks = value
                    .Portrait.Findings.Where(finding =>
                        finding.Action == PortraitFindingAction.Retake
                    )
                    .ToArray(),
                captureOverride = FaceQualityEvidence.Values(value.CaptureOverride),
                mouth = FaceQualityEvidence.Mouth(value.Portrait),
                background = FaceBackgroundEvidence.Create(value.Portrait.Background),
                opticalResolutionConfirmed = value.Acquisition.OpticalResolutionConfirmed,
                acquisitionFindings = value.AcquisitionFindings,
                opticalQualification = value.OpticalQualification.Match(
                    receipt => new[] { receipt },
                    () => Array.Empty<CameraOpticalQualification>()
                ),
            }
        );

    private static DesktopError Error(PackageError error) => new(error.Code, error.Message);
}
