// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture;
using OpenPhysical.Biometrics.Capture.Camera;
using OpenPhysical.Biometrics.Facial;
using OpenPhysical.Biometrics.Imaging;

internal sealed class FaceFramePresentation(Func<UnitResult<DesktopError>> changed)
{
    internal PreviewPublication<CameraFrame, DesktopError> Publication { get; } =
        new(new("Face.NoFrame", "Wait for a complete camera preview."));
    internal Maybe<FacePreviewScene> Scene { get; private set; }

    internal bool IsDisplayed(CameraFrame frame) =>
        Publication.Displayed.Match(value => ReferenceEquals(value, frame), () => false);

    internal UnitResult<DesktopError> Present(CameraFrame frame)
    {
        var previous = Publication.Displayed;
        var shown = Publication.Display(
            frame,
            Publication.Generation,
            value =>
                value
                    .Image.CopyRgbBytes()
                    .MapError(error => new DesktopError(error.Code, error.Message))
                    .Bind(rgb =>
                    {
                        try
                        {
                            return ReplaceScene(rgb, value.Image.Width, value.Image.Height);
                        }
                        finally
                        {
                            Array.Clear(rgb);
                        }
                    })
        );
        if (IsDisplayed(frame))
            previous.Tap(value => value.Dispose());
        else
            frame.Dispose();
        return shown.Bind(changed);
    }

    internal UnitResult<DesktopError> ShowDraft(FaceCaptureDraft Draft)
    {
        if (Draft.Analysis.HasNoValue)
            return UnitResult.Success<DesktopError>();
        var result = !Draft.Portrait.Match(value => !value.RgbPixels.IsEmpty, () => false)
            ? Show(
                Draft.Analysis.Value.Image.RgbPixels.AsSpan(),
                Draft.Analysis.Value.Width,
                Draft.Analysis.Value.Height
            )
            : Show(
                Draft.Portrait.Value.RgbPixels.AsSpan(),
                PortraitOutput.Width,
                PortraitOutput.Height
            );
        return result;
    }

    private UnitResult<DesktopError> Show(ReadOnlySpan<byte> rgb, int width, int height) =>
        ReplaceScene(rgb, width, height).Bind(changed);

    private UnitResult<DesktopError> ReplaceScene(ReadOnlySpan<byte> rgb, int width, int height)
    {
        var next = FacePreviewScene.Create(rgb, width, height);
        if (next.IsFailure)
            return UnitResult.Failure(next.Error);
        var previous = Scene;
        Scene = next.Value;
        previous.Tap(value => value.Dispose());
        return UnitResult.Success<DesktopError>();
    }

    internal UnitResult<DesktopError> Clear()
    {
        var shown = Publication.Displayed;
        Publication.Forget();
        shown.Tap(value => value.Dispose());
        Scene.Tap(value => value.Dispose());
        Scene = default;
        return changed();
    }
}
