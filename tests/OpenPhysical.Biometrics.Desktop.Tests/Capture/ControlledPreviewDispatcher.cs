// SPDX-License-Identifier: GPL-2.0-or-later

using System.Threading.Channels;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture;

namespace OpenPhysical.Biometrics.Desktop.Tests;

internal sealed class ControlledPreviewDispatcher : IPreviewDispatcher
{
    private readonly Channel<PreviewUpdate> updates = Channel.CreateUnbounded<PreviewUpdate>();

    internal Maybe<CaptureFrame> Displayed { get; private set; }

    public Task<UnitResult<DesktopError>> DisplayAsync(PreviewUpdate update)
    {
        _ = updates.Writer.TryWrite(update);
        return Task.FromResult(UnitResult.Success<DesktopError>());
    }

    public Task<UnitResult<DesktopError>> ReportAsync(DesktopError error) =>
        Task.FromResult(UnitResult.Success<DesktopError>());

    internal Task<PreviewUpdate> NextAsync() =>
        updates.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));

    internal UnitResult<DesktopError> Render(PreviewUpdate update) =>
        update.Display(frame =>
        {
            Displayed = frame;
            return UnitResult.Success<DesktopError>();
        });
}
