// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic;

using CSharpFunctionalExtensions;

internal sealed class Fs50WholeContactLifetime(Fs50WholeContactPolicy policy, TimeProvider clock)
{
    private Maybe<Fs50EmptySensorKey> session;
    private Maybe<long> last;
    private int emptyFrames;

    internal bool Track(Fs50EmptySensorKey current)
    {
        var now = clock.GetTimestamp();
        var changed =
            (session.HasValue && !session.Value.SameSession(current))
            || (
                last.HasValue
                && clock.GetElapsedTime(last.Value, now) > policy.Presentation.AcquisitionGap
            );
        if (changed)
            emptyFrames = 0;
        session = current;
        last = now;
        return changed;
    }

    internal bool ObserveEmpty(Fs50WholeContactInput input, Fs50EmptySensorObservation sensor)
    {
        var empty =
            sensor.ConfidentlyEmpty
            && input.Region.Issue == Fs50ContactIssue.None
            && input.Region.Diagnostics.CandidateArea == 0
            && input.Region.Diagnostics.RidgeSupportArea == 0
            && Fs50EmptySensorDifference.MeanGray(input.Frame.Pixels.AsSpan())
                >= policy.EmptySensor.Options.MinimumEmptyMeanGray;
        emptyFrames = empty ? Math.Min(policy.Presentation.AbsenceFrames, emptyFrames + 1) : 0;
        return emptyFrames >= policy.Presentation.AbsenceFrames;
    }

    internal bool Contact()
    {
        emptyFrames = 0;
        return true;
    }

    internal bool Clear()
    {
        session = Maybe<Fs50EmptySensorKey>.None;
        last = Maybe<long>.None;
        emptyFrames = 0;
        return true;
    }
}
