// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture;

using CSharpFunctionalExtensions;

internal sealed class FingerPresentationChange
{
    private const double MinimumChange = .6;
    private const double MaximumSettledMotion = .5;
    private Maybe<FingerPresenceSample> consumed;
    private Maybe<FingerPresenceSample> previous;
    private bool observing;
    private int count;

    internal bool Consume(FingerPresenceSample sample)
    {
        consumed = sample;
        observing = false;
        return ClearPending();
    }

    internal bool Wait(Maybe<FingerPresenceSample> latest)
    {
        if (consumed.HasNoValue)
            consumed = latest;
        observing = consumed.HasValue;
        return ClearPending();
    }

    internal bool Observe(FingerPresenceSample sample, int requiredFrames)
    {
        if (!observing || consumed.HasNoValue || !consumed.Value.Compatible(sample.Frame))
            return false;
        var difference = FingerFrameMotion.Score(consumed.Value, sample);
        if (!double.IsFinite(difference) || difference < MinimumChange)
        {
            ClearPending();
            return false;
        }
        // New impressions must settle against each other, not just differ from the consumed image.
        var motion = previous.Match(
            value => FingerFrameMotion.Score(value, sample),
            () => double.PositiveInfinity
        );
        count = motion <= MaximumSettledMotion && motion < difference / 2 ? count + 1 : 1;
        previous = sample;
        if (count < requiredFrames)
            return false;
        observing = false;
        ClearPending();
        return true;
    }

    internal bool Clear()
    {
        consumed = default;
        observing = false;
        return ClearPending();
    }

    internal bool Interrupt() => ClearPending();

    private bool ClearPending()
    {
        previous = default;
        count = 0;
        return true;
    }
}
