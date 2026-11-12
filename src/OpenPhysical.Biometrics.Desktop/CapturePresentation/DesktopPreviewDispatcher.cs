// SPDX-License-Identifier: GPL-2.0-or-later
using Avalonia.Threading;
using CSharpFunctionalExtensions;

namespace OpenPhysical.Biometrics.Desktop;

/// <summary>Presents scanner updates on the Avalonia UI thread and acknowledges rendered frames.</summary>
public sealed class DesktopPreviewDispatcher : IPreviewDispatcher
{
    private Maybe<CaptureViewModel> presentation;

    internal UnitResult<DesktopError> Attach(CaptureViewModel viewModel)
    {
        presentation = viewModel;
        return UnitResult.Success<DesktopError>();
    }

    /// <inheritdoc/>
    public async Task<UnitResult<DesktopError>> DisplayAsync(PreviewUpdate update)
    {
        var result = await DesktopEdge.TryAsync(
            async () =>
                await Dispatcher.UIThread.InvokeAsync(() =>
                    presentation.Match(
                        view => Present(view, update),
                        () =>
                            UnitResult.Failure(
                                new DesktopError(
                                    "PresentationMissing",
                                    "Capture view is unavailable."
                                )
                            )
                    )
                ),
            "DisplayFailed",
            "Cannot update preview."
        );
        return result.IsSuccess ? result.Value : UnitResult.Failure(result.Error);
    }

    private static UnitResult<DesktopError> Present(CaptureViewModel view, PreviewUpdate update)
    {
        var displayed = update.Display(frame =>
            view.Display(
                frame,
                update.Segmentation,
                update.Processing,
                update.Ready,
                update.WholeContactProof
            )
        );
        if (displayed.IsSuccess && update.Acknowledged.HasValue)
            displayed = view.AcknowledgeDisplayed(
                update.Acknowledged.Value,
                update.Segmentation,
                update.Ready,
                update.WholeContactProof,
                update.AdjustingExposure,
                update.ConfidentlyEmpty
            );
        view.RefreshCommands();
        return displayed;
    }

    /// <inheritdoc/>
    public async Task<UnitResult<DesktopError>> ReportAsync(DesktopError error)
    {
        var result = await DesktopEdge.TryAsync(
            async () =>
                await Dispatcher.UIThread.InvokeAsync(() =>
                    presentation.Match(
                        view => view.SourceError(error),
                        () =>
                            UnitResult.Failure(
                                new DesktopError(
                                    "PresentationMissing",
                                    "Capture view is unavailable."
                                )
                            )
                    )
                ),
            "DisplayFailed",
            "Cannot update device status."
        );
        return result.IsSuccess ? result.Value : UnitResult.Failure(result.Error);
    }
}
