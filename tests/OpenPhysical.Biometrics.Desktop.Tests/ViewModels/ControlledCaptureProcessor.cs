// SPDX-License-Identifier: GPL-2.0-or-later
using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.DependencyInjection;
using OpenPhysical.Biometrics.Capture;
using OpenPhysical.Biometrics.Fingerprints;
using OpenPhysical.Biometrics.NBIS;

namespace OpenPhysical.Biometrics.Desktop.Tests;

internal sealed class ControlledCaptureProcessor : ICaptureProcessor, IDisposable
{
    private readonly ServiceProvider services;
    private readonly CaptureProcessor inner;
    private readonly TaskCompletionSource<bool> release = new(
        TaskCreationOptions.RunContinuationsAsynchronously
    );

    internal ControlledCaptureProcessor()
    {
        services = new ServiceCollection()
            .AddFingerprintProcessing()
            .AddNfiq1FingerprintQuality()
            .AddNfiq2FingerprintQuality()
            .BuildServiceProvider();
        var native = new ControlledNbisEngine(
            services.GetRequiredService<IFingerprintEngine>(),
            Enter
        );
        inner = new CaptureProcessor(
            new FingerprintPreparationService(
                native,
                services.GetRequiredService<IFingerprintQualityProvider>()
            ),
            services.GetRequiredService<IFingerprintUnifiedQualityProvider>()
        );
    }

    internal TaskCompletionSource<FingerprintImage> Entered { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal bool Release() => release.TrySetResult(true);

    private bool Enter(FingerprintImage image)
    {
        Entered.TrySetResult(image);
        return release.Task.GetAwaiter().GetResult();
    }

    public Result<CapturePreparedFrame, DesktopError> PrepareFrame(
        CaptureFrame frame,
        CaptureProcessingRequest request,
        CancellationToken token
    ) => inner.PrepareFrame(frame, request, token);

    public Result<CaptureCandidateEvaluation, DesktopError> Assess(
        CapturePreparedFrame frame,
        CancellationToken token
    ) => inner.Assess(frame, token);

    public Result<CaptureBurstSelection, DesktopError> EvaluateFrame(
        CaptureFrame frame,
        CaptureProcessingRequest request,
        CancellationToken token
    ) => inner.EvaluateFrame(frame, request, token);

    public Result<CaptureBurstSelection, DesktopError> EvaluateBurst(
        ImmutableArray<FingerCaptureCandidate> frames,
        CaptureProcessingRequest request,
        FingerprintQualitySelectionPolicy policy,
        CancellationToken token
    ) => inner.EvaluateBurst(frames, request, policy, token);

    public Result<CaptureBurstSelection, DesktopError> EvaluateArtifact(
        CapturePreparedFrame frame,
        FingerprintQualitySelectionPolicy policy,
        CancellationToken token
    ) => inner.EvaluateArtifact(frame, policy, token);

    public Result<PreparedFingerprint, DesktopError> ExtractPrepared(
        CaptureCandidateEvaluation candidate,
        CancellationToken token
    ) => inner.ExtractPrepared(candidate, token);

    public Result<CapturedFingerprint, DesktopError> CompleteCapture(
        CaptureBurstSelection selection,
        PreparedFingerprint prepared,
        FingerPosition finger,
        Maybe<CaptureImageQualityEvidence> original,
        CancellationToken token
    ) => inner.CompleteCapture(selection, prepared, finger, original, token);

    public Result<CapturedFingerprint, DesktopError> Extract(
        CaptureBurstSelection selection,
        FingerPosition finger,
        Maybe<CaptureImageQualityEvidence> original,
        CancellationToken token
    ) => inner.Extract(selection, finger, original, token);

    public void Dispose()
    {
        Release();
        services.Dispose();
    }
}
