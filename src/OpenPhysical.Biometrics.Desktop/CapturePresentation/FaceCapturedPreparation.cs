// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture.Camera;
using OpenPhysical.Biometrics.Facial;

internal sealed class FaceCapturedPreparation(FaceCaptureProcessing processor)
{
    private Maybe<CameraFrame> frame;
    internal Maybe<PortraitAnalysis> Analysis { get; private set; }

    internal UnitResult<DesktopError> Remember(CameraFrame source, PortraitAnalysis analysis)
    {
        frame = source;
        Analysis = analysis;
        return UnitResult.Success<DesktopError>();
    }

    internal UnitResult<DesktopError> Clear()
    {
        frame = default;
        Analysis = default;
        return UnitResult.Success<DesktopError>();
    }

    internal Task<
        Result<(PortraitAnalysis Analysis, FacePreparedReview Portrait), DesktopError>
    > PrepareAsync(CameraFrame source) => Task.Run(() => Prepare(source));

    internal Result<(PortraitAnalysis Analysis, FacePreparedReview Portrait), DesktopError> Prepare(
        CameraFrame source
    )
    {
        var mouth = processor.MouthPolicy;
        var background = processor.BackgroundPolicy;
        return (
            frame.Match(value => ReferenceEquals(value, source), () => false)
                ? Analysis.ToResult(new DesktopError("Face.Analysis", "The face was not analyzed."))
                : processor.Analyze(source)
        )
            .Bind(analysis => processor.PrepareFrozen(analysis, background))
            .Bind(analysis =>
                processor
                    .PrepareReview(analysis, mouth, background)
                    .Map(portrait => (analysis, portrait))
            );
    }

    internal static string Guidance(PortraitAnalysis analysis) =>
        analysis.Faces.Length == 1 ? "Review the portrait, then accept the face."
        : analysis.Faces.IsEmpty ? "No face found. Retake with your full face in view."
        : "More than one face found. Retake with one person in view.";
}
