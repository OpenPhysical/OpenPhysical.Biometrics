// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture;
using OpenPhysical.Biometrics.Capture.Futronic;
using OpenPhysical.Biometrics.Fingerprints;

internal sealed class CaptureAdjustmentWork(
    CaptureImagePreparation images,
    ICaptureProcessor processor,
    Func<Task<UnitResult<DesktopError>>> ensureFrozen
)
{
    private Maybe<CancellationTokenSource> pending;
    private readonly object sync = new();
    private readonly List<Task<UnitResult<DesktopError>>> running = [];
    internal Maybe<CapturedFingerprint> Source { get; private set; }

    internal UnitResult<DesktopError> Cancel(bool clearSource)
    {
        pending.Tap(value =>
        {
            value.Cancel();
            value.Dispose();
        });
        pending = Maybe<CancellationTokenSource>.None;
        if (clearSource)
            Source = Maybe<CapturedFingerprint>.None;
        return UnitResult.Success<DesktopError>();
    }

    internal Task<UnitResult<DesktopError>> RunAsync(
        CaptureFrame frame,
        CaptureAdjustmentEvidence evidence,
        FingerPosition finger,
        CapturePolicySettings policy,
        Maybe<CapturedFingerprint> source,
        bool assess,
        Func<CaptureAdjustmentPrepared, bool> show,
        Func<Result<CapturedFingerprint, DesktopError>, UnitResult<DesktopError>> complete
    )
    {
        var task = RunCoreAsync(frame, evidence, finger, policy, source, assess, show, complete);
        lock (sync)
        {
            running.RemoveAll(item => item.IsCompleted);
            running.Add(task);
        }
        return task;
    }

    internal async Task<UnitResult<DesktopError>> DrainAsync()
    {
        Cancel(true);
        Task<UnitResult<DesktopError>>[] tasks;
        lock (sync)
            tasks = [.. running];
        var results = await Task.WhenAll(tasks);
        lock (sync)
            running.Clear();
        return results.FirstOrDefault(
            result => result.IsFailure,
            UnitResult.Success<DesktopError>()
        );
    }

    private async Task<UnitResult<DesktopError>> RunCoreAsync(
        CaptureFrame frame,
        CaptureAdjustmentEvidence evidence,
        FingerPosition finger,
        CapturePolicySettings policy,
        Maybe<CapturedFingerprint> source,
        bool assess,
        Func<CaptureAdjustmentPrepared, bool> show,
        Func<Result<CapturedFingerprint, DesktopError>, UnitResult<DesktopError>> complete
    )
    {
        var token = Begin(source);
        var original = Source
            .Bind(value => value.Processing)
            .Map(value => value.OriginalAssessment);
        var result = await DesktopEdge.TryAsync(
            () =>
                ApplyAsync(
                    frame,
                    evidence,
                    finger,
                    policy,
                    original,
                    assess,
                    show,
                    complete,
                    token
                ),
            "Capture.Adjustment",
            "The adjusted image could not be processed."
        );
        return result.IsSuccess ? result.Value
            : result.Error.Code == "Cancelled" ? UnitResult.Success<DesktopError>()
            : UnitResult.Failure(result.Error);
    }

    private CancellationToken Begin(Maybe<CapturedFingerprint> source)
    {
        Cancel(false);
        if (source.HasValue)
            Source = source;
        var cancellation = new CancellationTokenSource();
        pending = cancellation;
        return cancellation.Token;
    }

    private async Task<UnitResult<DesktopError>> ApplyAsync(
        CaptureFrame frame,
        CaptureAdjustmentEvidence evidence,
        FingerPosition finger,
        CapturePolicySettings policy,
        Maybe<CaptureImageQualityEvidence> original,
        bool assess,
        Func<CaptureAdjustmentPrepared, bool> show,
        Func<Result<CapturedFingerprint, DesktopError>, UnitResult<DesktopError>> complete,
        CancellationToken token
    )
    {
        var drained = await DrainAcquisitionAsync(assess);
        if (drained.IsFailure)
            return drained;
        if (token.IsCancellationRequested)
            return UnitResult.Success<DesktopError>();
        var prepared = await Task.Run(() => Prepare(frame, evidence, policy, assess, token));
        if (token.IsCancellationRequested)
            return UnitResult.Success<DesktopError>();
        if (prepared.IsFailure)
            return UnitResult.Failure(prepared.Error);
        if (!images.IsCurrent(prepared.Value.Artifact) || !show(prepared.Value) || !assess)
            return UnitResult.Success<DesktopError>();
        await Task.Delay(TimeSpan.FromMilliseconds(150), token);
        var processed = await new CaptureQualityWork(processor).ReprocessAsync(
            prepared.Value.Artifact,
            finger,
            policy,
            original,
            prepared.Value.Evidence,
            show,
            token
        );
        if (token.IsCancellationRequested || !images.IsCurrent(prepared.Value.Artifact))
            return UnitResult.Success<DesktopError>();
        if (processed.IsSuccess)
            Source = processed.Value;
        return complete(processed);
    }

    private async Task<UnitResult<DesktopError>> DrainAcquisitionAsync(bool assess) =>
        assess ? await ensureFrozen() : UnitResult.Success<DesktopError>();

    private Result<CaptureAdjustmentPrepared, DesktopError> Prepare(
        CaptureFrame frame,
        CaptureAdjustmentEvidence evidence,
        CapturePolicySettings policy,
        bool assess,
        CancellationToken token
    )
    {
        var snapshot = images.Snapshot();
        return assess
            ? evidence
                .Prepare(
                    frame,
                    new(
                        snapshot.Policy,
                        policy.Evidence,
                        policy.Contour,
                        snapshot.Revision,
                        evidence.Input
                    ),
                    token
                )
                .Map(prepared => new CaptureAdjustmentPrepared(
                    prepared.Artifact,
                    CaptureAdjustmentEvidence.From(prepared)
                ))
            : images
                .PreparePreview(frame, snapshot, token)
                .Map(artifact => new CaptureAdjustmentPrepared(artifact, evidence));
    }
}
