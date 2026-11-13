// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;

/// <summary>Times a finger presentation and selects one recent acknowledged image for extraction.</summary>
/// <param name="clock">Monotonic elapsed-time source.</param>
/// <remarks>Only sustained major position changes reset timing. Pixel/exposure variation ranks candidates.</remarks>
public sealed class FingerPresenceGate(TimeProvider clock)
{
    private readonly FingerCaptureBuffer buffer = new(clock);
    private readonly FingerPresentationChange replacement = new();
    private Maybe<FingerPresenceSample> latest;
    private Maybe<FingerPresenceSample> anchor;
    private Maybe<long> last;
    private TimeSpan delay = TimeSpan.FromSeconds(1);
    private bool requireRemoval;
    private int absent;
    private int major;

    /// <summary>Gets the number of retained eligible displayed images, at most five.</summary>
    public int CandidateCount => buffer.Count;

    /// <summary>Gets the bounded immutable acknowledged candidate snapshot, without re-aging after assessment.</summary>
    public ImmutableArray<FingerCaptureCandidate> Candidates => buffer.Candidates;

    /// <summary>Changes application policy and restarts timing while preserving the removal latch.</summary>
    /// <param name="policy">Validated policy.</param>
    /// <returns>A successful policy update.</returns>
    public UnitResult<CaptureError> ConfigurePolicy(FingerCapturePolicy policy)
    {
        if (buffer.Policy != policy)
            ClearInterval();
        buffer.Policy = policy;
        return UnitResult.Success<CaptureError>();
    }

    /// <summary>Observes an image only after its successful presentation acknowledgement.</summary>
    /// <param name="frame">Unmodified acknowledged image.</param>
    /// <param name="foregroundMask">Foreground from exactly this image, or empty when absent.</param>
    /// <param name="componentCount">Substantial foreground components in this image.</param>
    /// <returns>Latest eligibility, countdown, and an immutable best-frame selection at the deadline.</returns>
    public Result<FingerPresenceObservation, CaptureError> Observe(
        CaptureFrame frame,
        ImmutableArray<byte> foregroundMask,
        int componentCount
    ) => Observe(frame, foregroundMask, componentCount, true);

    /// <summary>Tracks presentation and removal, optionally suppressing automatic triggering.</summary>
    /// <param name="frame">Unmodified acknowledged image.</param>
    /// <param name="foregroundMask">Foreground from this exact image, or empty when absent.</param>
    /// <param name="componentCount">Substantial foreground components.</param>
    /// <param name="automatic">Whether the deadline may trigger and consume the presentation.</param>
    /// <returns>Presence and removal state; manual observations never trigger or create a latch.</returns>
    public Result<FingerPresenceObservation, CaptureError> Observe(
        CaptureFrame frame,
        ImmutableArray<byte> foregroundMask,
        int componentCount,
        bool automatic
    )
    {
        if (clock is null || !FingerPresenceInput.Valid(frame, foregroundMask, componentCount))
            return FingerPresenceInput.Invalid();
        var present = !foregroundMask.IsDefaultOrEmpty && foregroundMask.Any(value => value != 0);
        if (present != (componentCount > 0))
            return FingerPresenceInput.Invalid();
        var now = clock.GetTimestamp();
        var cleared =
            last.HasValue && clock.GetElapsedTime(last.Value, now) > buffer.Policy.AcquisitionGap;
        if (cleared)
            ClearInterval();
        last = now;
        buffer.Prune(now);
        if (!present)
        {
            replacement.Interrupt();
            if (++absent >= buffer.Policy.AbsenceFrames)
            {
                ClearInterval();
                requireRemoval = false;
                replacement.Clear();
                latest = default;
                cleared = true;
            }
            return Observation(false, false, false, now, cleared);
        }
        absent = 0;
        if (componentCount != 1)
        {
            ClearInterval();
            return Observation(true, false, false, now, true);
        }
        return ObserveSingle(frame, foregroundMask, now, cleared, automatic);
    }

    private FingerPresenceObservation ObserveSingle(
        CaptureFrame frame,
        ImmutableArray<byte> foregroundMask,
        long now,
        bool cleared,
        bool automatic
    )
    {
        var sample = FingerPresenceSample.Create(frame, foregroundMask, now);
        if (
            requireRemoval
            && replacement.Observe(sample, Math.Max(2, buffer.Policy.RepositionFrames))
        )
        {
            requireRemoval = false;
            cleared |= ClearInterval();
        }
        latest = sample;
        cleared |= UpdatePresentation(sample);
        buffer.Add(sample);
        var ready =
            automatic && !requireRemoval && clock.GetElapsedTime(anchor.Value.At, now) >= delay;
        if (ready)
        {
            requireRemoval = true;
            replacement.Consume(sample);
        }
        return Observation(true, true, ready, now, cleared);
    }

    /// <summary>Re-arms capture and clears all retained images.</summary>
    /// <param name="requireRemoval">True after accepting a finger; false for an explicit retry.</param>
    /// <returns>A successful reset.</returns>
    public UnitResult<CaptureError> Reset(bool requireRemoval)
    {
        if (requireRemoval)
            replacement.Wait(latest);
        else
        {
            replacement.Clear();
            latest = default;
        }
        ClearInterval();
        last = Maybe<long>.None;
        absent = 0;
        this.requireRemoval = requireRemoval;
        return UnitResult.Success<CaptureError>();
    }

    /// <summary>Updates the bounded capture delay, clearing old timing and candidates when changed.</summary>
    /// <param name="value">Zero through ten seconds.</param>
    /// <returns>Success or a precise invalid-delay failure.</returns>
    public UnitResult<CaptureError> ConfigureDelay(TimeSpan value)
    {
        if (value < TimeSpan.Zero || value > TimeSpan.FromSeconds(10))
            return UnitResult.Failure(
                new CaptureError(
                    CaptureErrorCodes.InvalidArgument,
                    "Automatic capture delay must be from 0 to 10 seconds."
                )
            );
        if (value != delay)
            ClearInterval();
        delay = value;
        return UnitResult.Success<CaptureError>();
    }

    private bool UpdatePresentation(FingerPresenceSample sample)
    {
        var cleared = anchor.HasValue && !anchor.Value.Compatible(sample.Frame);
        if (cleared)
            ClearInterval();
        if (anchor.HasValue)
        {
            major = sample.MajorFrom(anchor.Value, buffer.Policy) ? major + 1 : 0;
            if (major >= buffer.Policy.RepositionFrames)
            {
                ClearInterval();
                cleared = true;
            }
        }
        if (anchor.HasNoValue)
            anchor = sample;
        return cleared;
    }

    private FingerPresenceObservation Observation(
        bool present,
        bool single,
        bool trigger,
        long now,
        bool cleared
    )
    {
        var elapsed = anchor.HasValue ? clock.GetElapsedTime(anchor.Value.At, now) : TimeSpan.Zero;
        return new(present, single, trigger)
        {
            Remaining = elapsed >= delay ? TimeSpan.Zero : delay - elapsed,
            BestFrame = trigger ? buffer.Best : Maybe<FingerCaptureCandidate>.None,
            PresentationCleared = cleared,
            RequiresRemoval = requireRemoval,
        };
    }

    private bool ClearInterval()
    {
        replacement.Interrupt();
        anchor = Maybe<FingerPresenceSample>.None;
        buffer.Clear();
        major = 0;
        return true;
    }
}
