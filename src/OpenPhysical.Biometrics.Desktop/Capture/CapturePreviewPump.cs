// SPDX-License-Identifier: GPL-2.0-or-later

using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using OpenPhysical.Biometrics.Capture;
using OpenPhysical.Biometrics.Capture.Futronic;

namespace OpenPhysical.Biometrics.Desktop;

internal sealed class CapturePreviewPump(
    CapturePreviewState state,
    CaptureDeviceConnection connection,
    IPreviewDispatcher dispatcher,
    TimeProvider clock,
    ILogger<CaptureCoordinator> logger,
    CaptureImagePreparation images,
    CaptureSensorEvidence evidence
)
{
    private readonly CaptureRecoveryPolicy recovery = new();

    internal bool ResetRecovery() => recovery.Reset();

    internal async Task RunAsync(CancellationToken token)
    {
        var protectedRun = await DesktopEdge
            .TryAsync(
                async () =>
                {
                    await RunCoreAsync(token).ConfigureAwait(false);
                    return true;
                },
                "Capture.Preview",
                "The scanner preview could not continue."
            )
            .ConfigureAwait(false);
        if (protectedRun.IsFailure && !token.IsCancellationRequested)
        {
            _ = state.Pause();
            await ReportAsync(protectedRun.Error).ConfigureAwait(false);
        }
    }

    private async Task<bool> RunCoreAsync(CancellationToken token)
    {
        while (state.IsLive && !token.IsCancellationRequested)
        {
            var opened = await connection
                .EnsureOpenAsync(state.Exposure.Settings, token)
                .ConfigureAwait(false);
            if (opened.IsFailure)
            {
                var retry = recovery.Retry(opened.Error);
                if (!retry)
                    state.Pause();
                if (!token.IsCancellationRequested)
                    await ReportAsync(opened.Error).ConfigureAwait(false);
                if (!retry)
                    break;
                await Task.Delay(TimeSpan.FromSeconds(1), clock, token).ConfigureAwait(false);
                continue;
            }
            var frame = await AcquireAsync(connection.Session.Value, token).ConfigureAwait(false);
            if (frame.IsFailure)
            {
                await RecoverAsync(frame.Error, token).ConfigureAwait(false);
                continue;
            }
            if (!state.IsLive || token.IsCancellationRequested)
                break;
            var presented = await PresentAsync(frame.Value).ConfigureAwait(false);
            if (presented.IsSuccess)
                recovery.Reset();
            if (presented.IsFailure)
            {
                _ = state.Pause();
                await ReportAsync(presented.Error).ConfigureAwait(false);
            }
        }
        return true;
    }

    private async Task<bool> RecoverAsync(DesktopError error, CancellationToken token)
    {
        evidence.Clear();
        var closed = await connection.CloseAsync().ConfigureAwait(false);
        if (closed.IsFailure || !recovery.Retry(error))
            _ = state.Pause();
        if (!token.IsCancellationRequested)
            await ReportAsync(closed.IsFailure ? closed.Error : error).ConfigureAwait(false);
        if (state.IsLive && !token.IsCancellationRequested)
            await Task.Delay(TimeSpan.FromSeconds(1), clock, token).ConfigureAwait(false);
        return true;
    }

    private async Task<Result<CaptureFrame, DesktopError>> AcquireAsync(
        IFingerprintCaptureSession session,
        CancellationToken token
    )
    {
        var settings = await session
            .ApplySettingsAsync(state.Exposure.Settings, token)
            .ConfigureAwait(false);
        if (settings.IsFailure)
            return new DesktopError(settings.Error.Code, settings.Error.Message);
        var frame = await session.CaptureAsync(token).ConfigureAwait(false);
        return frame.IsSuccess
            ? Result.Success<CaptureFrame, DesktopError>(frame.Value)
            : Result.Failure<CaptureFrame, DesktopError>(
                new(frame.Error.Code, frame.Error.Message)
            );
    }

    private async Task<UnitResult<DesktopError>> PresentAsync(CaptureFrame frame)
    {
        var processingPolicy = images.Snapshot();
        var contact = evidence.Observe(frame, connection.Session.Value);
        if (contact.IsFailure)
            return contact.Error;
        var adjusted = state.Exposure.Adjust(
            frame,
            contact.Value.Contact,
            contact.Value.WholeContactProof
        );
        if (adjusted.IsFailure)
            return adjusted.Error;
        var processed = images.PreparePreview(frame, processingPolicy, CancellationToken.None);
        if (processed.IsFailure)
            return processed.Error;
        return await dispatcher
            .DisplayAsync(
                new PreviewUpdate(
                    state,
                    frame,
                    state.Generation,
                    Maybe.From(contact.Value.Contact),
                    Maybe.From(processed.Value),
                    contact.Value.Ready,
                    contact.Value.WholeContactProof,
                    adjusted.Value,
                    contact.Value.ConfidentlyEmpty
                )
                {
                    IsCurrent = () => images.IsCurrent(processed.Value),
                }
            )
            .ConfigureAwait(false);
    }

    private async Task<bool> ReportAsync(DesktopError error)
    {
        logger.LogInformation("Capture status: {Code}", error.Code);
        _ = await DesktopEdge
            .TryAsync(
                () => dispatcher.ReportAsync(error),
                "Capture.Status",
                "The scanner status could not be displayed."
            )
            .ConfigureAwait(false);
        return true;
    }
}
