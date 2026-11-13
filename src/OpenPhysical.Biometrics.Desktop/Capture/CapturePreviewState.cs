// SPDX-License-Identifier: GPL-2.0-or-later

using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture;
using OpenPhysical.Biometrics.Capture.Futronic;

namespace OpenPhysical.Biometrics.Desktop;

internal sealed class CapturePreviewState
{
    private readonly object sync = new();
    private readonly PreviewPublication<CaptureFrame, DesktopError> publication = new(
        new DesktopError("Capture.NoFrame", "Wait for a complete preview frame.")
    );
    private Maybe<CaptureFrame> displayed => publication.Displayed;
    private Maybe<Fs50WholeContactProof> displayedProof;
    private Maybe<CaptureFrame> pendingFrozen;
    private long generation => publication.Generation;
    private bool live => publication.IsLive;
    private readonly DisplayedCaptureHistory history;
    internal CaptureExposureAccess Exposure { get; }

    internal CapturePreviewState(TimeProvider clock)
    {
        history = new(clock);
        Exposure = new(sync, () => live);
    }

    internal UnitResult<DesktopError> ConfigurePolicy(FingerCapturePolicy policy)
    {
        lock (sync)
            return history.ConfigurePolicy(policy);
    }

    internal bool IsLive
    {
        get
        {
            lock (sync)
                return live;
        }
    }

    internal bool HasDisplayedFrame
    {
        get
        {
            lock (sync)
                return displayed.HasValue;
        }
    }

    internal long Generation
    {
        get
        {
            lock (sync)
                return generation;
        }
    }

    internal bool Begin()
    {
        lock (sync)
        {
            publication.Begin();
            displayedProof = Maybe<Fs50WholeContactProof>.None;
            pendingFrozen = Maybe<CaptureFrame>.None;
            history.Clear();
            return true;
        }
    }

    internal bool Pause()
    {
        lock (sync)
        {
            publication.Pause();
            pendingFrozen = Maybe<CaptureFrame>.None;
            return true;
        }
    }

    internal Result<CaptureFrame, DesktopError> Freeze()
    {
        lock (sync)
        {
            if (!displayed.HasValue)
                return new DesktopError("Capture.NoFrame", "Wait for a complete preview frame.");
            return publication.Freeze();
        }
    }

    internal Result<CaptureFrame, DesktopError> Freeze(CaptureFrame selected)
    {
        lock (sync)
        {
            if (pendingFrozen.HasValue && ReferenceEquals(pendingFrozen.Value, selected))
            {
                pendingFrozen = Maybe<CaptureFrame>.None;
                return selected;
            }
            if (!live || !history.Contains(selected))
                return new DesktopError(
                    "Capture.UnacknowledgedFrame",
                    "The automatic selection is not a recent displayed frame."
                );
            publication.Pause();
            return selected;
        }
    }

    internal bool ForgetDisplayed()
    {
        lock (sync)
        {
            publication.Forget();
            displayedProof = Maybe<Fs50WholeContactProof>.None;
            pendingFrozen = Maybe<CaptureFrame>.None;
            return history.Clear();
        }
    }

    internal UnitResult<DesktopError> Display(
        CaptureFrame frame,
        long expectedGeneration,
        Func<CaptureFrame, UnitResult<DesktopError>> render,
        bool candidate = true,
        Maybe<Fs50WholeContactProof> proof = default
    )
    {
        lock (sync)
        {
            return publication.Display(
                frame,
                expectedGeneration,
                value =>
                {
                    var result = render(value);
                    if (result.IsSuccess)
                    {
                        displayedProof = proof;
                        if (candidate)
                            history.Add(value, proof);
                    }
                    return result;
                }
            );
        }
    }

    internal Result<CaptureSelectionSnapshot, DesktopError> SnapshotWork(
        Maybe<CaptureFrame> selected,
        ImmutableArray<FingerCaptureCandidate> candidates
    )
    {
        lock (sync)
        {
            if (
                !live
                || displayed.HasNoValue
                || (selected.HasValue && !history.Contains(selected.Value))
            )
                return new DesktopError(
                    "Capture.NoFrame",
                    "Wait for an acknowledged qualified image."
                );
            var frame = selected.Or(displayed).Value;
            var snapshot = history.Snapshot(
                frame,
                candidates,
                ReferenceEquals(frame, displayed.Value)
                    ? displayedProof
                    : Maybe<Fs50WholeContactProof>.None
            );
            if (snapshot.IsSuccess)
            {
                pendingFrozen = frame;
                publication.Pause();
            }
            return snapshot;
        }
    }

    internal CaptureProcessingInput LiveInput(CaptureFrame frame)
    {
        lock (sync)
            return history.InputFor(frame, displayed, displayedProof);
    }
}
