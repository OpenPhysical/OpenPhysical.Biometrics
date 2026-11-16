// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic;

using CSharpFunctionalExtensions;

/// <summary>RAM-only, bounded learning and presentation trust scoped to one scanner session.</summary>
public sealed class Fs50EmptySensorLearning : IDisposable
{
    private readonly Fs50EmptySensorPolicy policy;
    private readonly Fs50EmptySensorRun run;
    private readonly List<Fs50EmptySensorReference> references = [];
    private Maybe<Fs50EmptySensorKey> session;
    private bool trusted;

    private Fs50EmptySensorLearning(Fs50EmptySensorPolicy policy, TimeProvider clock)
    {
        this.policy = policy;
        run = new Fs50EmptySensorRun(policy.Options, clock);
    }

    /// <summary>Creates validated learning state without reading or operating a scanner.</summary>
    /// <param name="policy">Validated learning choices.</param>
    /// <param name="clock">Monotonic observation clock.</param>
    /// <returns>Session-only learning state or invalid-argument failure.</returns>
    public static Result<Fs50EmptySensorLearning, CaptureError> Create(
        Fs50EmptySensorPolicy policy,
        TimeProvider clock
    ) =>
        policy is not null && clock is not null
            ? new Fs50EmptySensorLearning(policy, clock)
            : new CaptureError(
                CaptureErrorCodes.InvalidArgument,
                "Empty-sensor learning requires a policy and monotonic clock."
            );

    /// <summary>Observes exact-frame raw evidence; callers serialize observation and clearing.</summary>
    /// <param name="frame">Unchanged corrected acquisition.</param>
    /// <param name="contact">Contact evidence from that acquisition.</param>
    /// <param name="sessionId">Stable identity of the current open scanner session.</param>
    /// <returns>Readiness and compatible comparison evidence or a precise validation failure.</returns>
    public Result<Fs50EmptySensorObservation, CaptureError> Observe(
        CaptureFrame frame,
        Fs50ContactRegion contact,
        string sessionId
    )
    {
        var current = Fs50EmptySensorKey.Create(frame, sessionId);
        if (current.IsFailure)
            return current.Error;
        if (contact is null)
            return new CaptureError(
                CaptureErrorCodes.InvalidArgument,
                "Empty-sensor learning requires frame-associated contact evidence."
            );
        if (session.HasValue && !session.Value.SameSession(current.Value))
            _ = Clear();
        session = current.Value;
        var confidentlyEmpty = IsConfidentEmpty(frame, contact);
        if (confidentlyEmpty)
        {
            trusted = false;
            var learned = run.Observe(frame, current.Value);
            if (learned.HasValue)
                _ = Remember(learned.Value);
        }
        else
            _ = run.Clear();
        return Inspect(frame, contact, current.Value, confidentlyEmpty);
    }

    /// <summary>Discards pending pixels, all exposure references and presentation trust.</summary>
    /// <returns>Success after the session-only references have been released.</returns>
    public UnitResult<CaptureError> Clear()
    {
        _ = run.Clear();
        references.Clear();
        session = Maybe<Fs50EmptySensorKey>.None;
        trusted = false;
        return UnitResult.Success<CaptureError>();
    }

    /// <summary>Releases session-only references when the owner shuts down.</summary>
    public void Dispose() => _ = Clear();

    private bool IsConfidentEmpty(CaptureFrame frame, Fs50ContactRegion contact) =>
        !contact.Present
        && contact.Issue == Fs50ContactIssue.None
        && contact.Diagnostics.CandidateArea == 0
        && contact.Diagnostics.RidgeSupportArea == 0
        && Fs50EmptySensorDifference.MeanGray(frame.Pixels.AsSpan())
            >= policy.Options.MinimumEmptyMeanGray;

    private Fs50EmptySensorObservation Inspect(
        CaptureFrame frame,
        Fs50ContactRegion contact,
        Fs50EmptySensorKey current,
        bool confidentlyEmpty
    )
    {
        var matched = references.FindIndex(reference => reference.Key == current);
        Maybe<Fs50EmptySensorReference> reference =
            matched < 0 ? Maybe<Fs50EmptySensorReference>.None : references[matched];
        Maybe<double> difference = reference.HasValue
            ? Fs50EmptySensorDifference.Mean(
                frame.Pixels.AsSpan(),
                reference.Value.Pixels.AsSpan(),
                contact.Present ? contact.Mask.AsSpan() : ReadOnlySpan<byte>.Empty
            )
            : Maybe<double>.None;
        if (
            contact.Present
            && contact.Issue == Fs50ContactIssue.None
            && reference.HasValue
            && difference.Value >= policy.Options.MinimumPresentationMeanAbsoluteDifference
        )
            trusted = true;
        var ready =
            contact.Present ? trusted
            : contact.Issue == Fs50ContactIssue.None ? confidentlyEmpty && reference.HasValue
            : reference.HasValue || trusted;
        return new Fs50EmptySensorObservation(
            ready,
            trusted,
            references.Count,
            reference,
            difference,
            confidentlyEmpty
        );
    }

    private bool Remember(Fs50EmptySensorReference reference)
    {
        references.RemoveAll(existing => existing.Key == reference.Key);
        references.Add(reference);
        while (references.Count > policy.Options.MaximumReferences)
            references.RemoveAt(0);
        return true;
    }
}
