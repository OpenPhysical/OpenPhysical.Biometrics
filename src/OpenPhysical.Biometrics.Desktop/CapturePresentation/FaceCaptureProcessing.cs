// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture.Camera;
using OpenPhysical.Biometrics.Facial;
using OpenPhysical.Biometrics.Formats.Faces;
using OpenPhysical.Biometrics.Imaging;

internal sealed class FaceCaptureProcessing(
    PortraitPreparer portraits,
    IFacialRecordPreparer records
)
{
    internal PortraitMouthPolicy MouthPolicy { get; set; } = PortraitMouthPolicy.Default;
    internal PortraitBackgroundPolicy BackgroundPolicy { get; set; } =
        PortraitBackgroundPolicy.Default;

    internal Result<PortraitAnalysis, DesktopError> PrepareFrozen(
        PortraitAnalysis analysis,
        PortraitBackgroundPolicy background
    ) => background.Enabled ? portraits.PrepareBackground(analysis).MapError(Error) : analysis;

    internal Result<PortraitAnalysis, DesktopError> Analyze(CameraFrame frame)
    {
        var rgb = frame.Image.CopyRgbBytes();
        if (rgb.IsFailure)
            return new DesktopError(rgb.Error.Code, "The camera image is no longer available.");
        try
        {
            return PortraitImage
                .FromSrgb(rgb.Value, frame.Image.Width, frame.Image.Height)
                .Bind(portraits.Analyze)
                .MapError(Error);
        }
        finally
        {
            Array.Clear(rgb.Value);
        }
    }

    internal Result<PortraitPreparationResult, DesktopError> Reassess(
        PortraitPreparationResult portrait,
        PortraitMouthPolicy policy
    ) => PortraitCaptureChecks.Reassess(portrait, policy).MapError(Error);

    internal Result<FacePreparedReview, DesktopError> PrepareReview(
        PortraitAnalysis analysis,
        PortraitMouthPolicy mouth,
        PortraitBackgroundPolicy background
    ) =>
        portraits
            .Prepare(analysis, default, default, mouth, background)
            .MapError(Error)
            .Bind(portrait =>
                portrait.RgbPixels.IsEmpty
                    ? new FacePreparedReview(portrait, default)
                    : PortraitQualityAssessor
                        .Assess(portrait)
                        .MapError(Error)
                        .Map(quality => new FacePreparedReview(portrait, quality))
            );

    internal Result<FaceAcceptanceInput, DesktopError> Snapshot(
        FaceCaptureDraft draft,
        Maybe<CameraOpticalQualification> qualification
    )
    {
        if (draft.Source.HasNoValue || draft.Portrait.HasNoValue)
            return new DesktopError(
                "Face.NotReady",
                "Capture a source-supported portrait before accepting."
            );
        var source = draft.Source.Value;
        qualification = QualificationFor(source, qualification);
        var metadata = new FacialAcquisitionMetadata(
            source.CapturedAt,
            0,
            6,
            draft.ReviewConfirmed && draft.CaptureOverride.HasNoValue,
            qualification.HasValue,
            0
        );
        return new FaceAcceptanceInput(
            draft.Portrait.Value,
            metadata,
            source.Source,
            source.Mode,
            draft.Quality,
            FacialPreparationPolicy.UnsignedStaging with
            {
                CaptureOverride = draft.CaptureOverride,
            }
        )
        {
            OpticalQualification = qualification,
            PortraitReviewed = draft.ReviewConfirmed,
        };
    }

    private static Maybe<CameraOpticalQualification> QualificationFor(
        CameraFrame source,
        Maybe<CameraOpticalQualification> qualification
    ) =>
        qualification.Where(value =>
            CameraOpticalQualification.Matches(value, source.Source.Id, source.Mode)
        );

    internal Result<FaceAcceptedCapture, DesktopError> Accept(FaceAcceptanceInput input)
    {
        return records
            .Prepare(input.Portrait, input.Acquisition, input.Policy)
            .MapError(Error)
            .Ensure(
                value => value.Status == FacialPreparationStatus.Prepared && value.Record.HasValue,
                new DesktopError(
                    "Face.Profile",
                    "The portrait does not meet the facial object requirements."
                )
            )
            .Bind(value =>
                Incits385Codec
                    .Write(value.Record.Value)
                    .MapError(_ => new DesktopError(
                        "Face.Encode",
                        "The facial object could not be encoded."
                    ))
                    .Map(bytes => new FaceAcceptedCapture(
                        ImmutableArray.Create(bytes),
                        input.Portrait,
                        value.Acquisition,
                        input.Source,
                        input.Mode
                    )
                    {
                        Quality = input.Quality,
                        AcquisitionFindings = value.AcquisitionFindings,
                        OpticalQualification = input.OpticalQualification,
                        CaptureOverride = value.CaptureOverride,
                        PortraitReviewed = input.PortraitReviewed,
                    })
            );
    }

    private static DesktopError Error(FacialError error) => new(error.Code, error.Message);
}
